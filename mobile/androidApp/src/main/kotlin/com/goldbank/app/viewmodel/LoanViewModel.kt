package com.goldbank.app.viewmodel

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.goldbank.shared.data.local.SessionManager
import com.goldbank.shared.data.remote.grpc.AiGrpcClient
import com.goldbank.shared.data.remote.grpc.LoanGrpcClient
import com.goldbank.shared.domain.model.LoanApplicationResult
import com.goldbank.shared.domain.model.LoanDetail
import com.goldbank.shared.domain.model.LoanDocVerification
import com.goldbank.shared.domain.model.LoanEligibility
import com.goldbank.shared.domain.model.LoanScheduleEntry
import com.goldbank.shared.domain.model.LoanSummary
import com.goldbank.shared.domain.util.Result
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

class LoanViewModel(
    private val loanClient: LoanGrpcClient,
    private val sessionManager: SessionManager,
    private val aiClient: AiGrpcClient,
) : ViewModel() {

    private val _uiState = MutableStateFlow<LoanUiState>(LoanUiState.Idle)
    val uiState: StateFlow<LoanUiState> = _uiState.asStateFlow()

    private val _loans = MutableStateFlow<List<LoanSummary>>(emptyList())
    val loans: StateFlow<List<LoanSummary>> = _loans.asStateFlow()

    private val _loansLoading = MutableStateFlow(false)
    val loansLoading: StateFlow<Boolean> = _loansLoading.asStateFlow()

    private val _loansError = MutableStateFlow<String?>(null)
    val loansError: StateFlow<String?> = _loansError.asStateFlow()

    private val _loanDetail = MutableStateFlow<LoanDetail?>(null)
    val loanDetail: StateFlow<LoanDetail?> = _loanDetail.asStateFlow()

    private val _schedule = MutableStateFlow<List<LoanScheduleEntry>>(emptyList())
    val schedule: StateFlow<List<LoanScheduleEntry>> = _schedule.asStateFlow()

    private val _eligibility = MutableStateFlow<LoanEligibility?>(null)
    val eligibility: StateFlow<LoanEligibility?> = _eligibility.asStateFlow()

    private val _isCheckingEligibility = MutableStateFlow(false)
    val isCheckingEligibility: StateFlow<Boolean> = _isCheckingEligibility.asStateFlow()

    private val _eligibilityError = MutableStateFlow<String?>(null)
    val eligibilityError: StateFlow<String?> = _eligibilityError.asStateFlow()

    private val _loanDocVerification = MutableStateFlow<LoanDocVerification?>(null)
    val loanDocVerification: StateFlow<LoanDocVerification?> = _loanDocVerification.asStateFlow()

    private val _isVerifyingDoc = MutableStateFlow(false)
    val isVerifyingDoc: StateFlow<Boolean> = _isVerifyingDoc.asStateFlow()

    private val _docVerificationError = MutableStateFlow<String?>(null)
    val docVerificationError: StateFlow<String?> = _docVerificationError.asStateFlow()

    fun checkEligibility(amount: String, tenureMonths: Int, purpose: String) {
        _isCheckingEligibility.value = true
        _eligibility.value = null
        _eligibilityError.value = null
        viewModelScope.launch {
            val accountId = sessionManager.getAccountId() ?: run {
                _eligibilityError.value = "Session expired. Please log in again."
                _isCheckingEligibility.value = false
                return@launch
            }
            when (val result = aiClient.checkLoanEligibility(
                accountId = accountId,
                desiredAmount = amount,
                currency = "ZWG",
                tenureMonths = tenureMonths,
                purpose = purpose,
            )) {
                is Result.Success -> _eligibility.value = result.data
                is Result.Failure -> _eligibilityError.value = result.error.message
            }
            _isCheckingEligibility.value = false
        }
    }

    fun resetEligibility() {
        _eligibility.value = null
        _eligibilityError.value = null
    }

    fun verifyLoanDocuments(documentBytes: ByteArray, declaredIncome: String) {
        _isVerifyingDoc.value = true
        _loanDocVerification.value = null
        _docVerificationError.value = null
        viewModelScope.launch {
            val accountId = sessionManager.getAccountId() ?: run {
                _docVerificationError.value = "Session expired. Please log in again."
                _isVerifyingDoc.value = false
                return@launch
            }
            when (val result = aiClient.verifyPayslip(
                accountId = accountId,
                documentImage = documentBytes,
                declaredIncome = declaredIncome,
            )) {
                is Result.Success -> _loanDocVerification.value = result.data
                is Result.Failure -> _docVerificationError.value = result.error.message
            }
            _isVerifyingDoc.value = false
        }
    }

    fun resetDocVerification() {
        _loanDocVerification.value = null
        _docVerificationError.value = null
    }

    fun applyForLoan(
        amount: String,
        currency: String = "ZWG",
        tenureMonths: Int,
        purpose: String,
        pin: String,
        collateralAssetIds: List<String> = emptyList(),
    ) {
        _uiState.value = LoanUiState.Loading
        viewModelScope.launch {
            val accountId = sessionManager.getAccountId() ?: return@launch
            when (val result = loanClient.applyForLoan(
                accountId = accountId,
                amount = amount,
                currency = currency,
                tenureMonths = tenureMonths,
                purpose = purpose,
                pin = pin,
                collateralAssetIds = collateralAssetIds,
            )) {
                is Result.Success -> {
                    if (result.data.success) {
                        _uiState.value = LoanUiState.ApplicationSuccess(result.data)
                    } else {
                        _uiState.value = LoanUiState.Error(result.data.message)
                    }
                }
                is Result.Failure -> _uiState.value = LoanUiState.Error(result.error.message)
            }
        }
    }

    fun loadLoans(statusFilter: String = "") {
        _loansLoading.value = true
        _loansError.value = null
        viewModelScope.launch {
            val accountId = sessionManager.getAccountId() ?: return@launch
            when (val result = loanClient.listLoans(accountId, statusFilter)) {
                is Result.Success -> _loans.value = result.data
                is Result.Failure -> _loansError.value = result.error.message
            }
            _loansLoading.value = false
        }
    }

    fun loadLoanDetail(loanId: String) {
        _loansLoading.value = true
        viewModelScope.launch {
            val accountId = sessionManager.getAccountId() ?: return@launch
            when (val result = loanClient.getLoan(loanId, accountId)) {
                is Result.Success -> _loanDetail.value = result.data
                is Result.Failure -> _loansError.value = result.error.message
            }
            _loansLoading.value = false
        }
    }

    fun loadSchedule(loanId: String) {
        viewModelScope.launch {
            val accountId = sessionManager.getAccountId() ?: return@launch
            when (val result = loanClient.getLoanSchedule(loanId, accountId)) {
                is Result.Success -> _schedule.value = result.data
                is Result.Failure -> {}
            }
        }
    }

    fun resetState() { _uiState.value = LoanUiState.Idle }
}

sealed interface LoanUiState {
    data object Idle : LoanUiState
    data object Loading : LoanUiState
    data class ApplicationSuccess(val result: LoanApplicationResult) : LoanUiState
    data class Error(val message: String) : LoanUiState
}
