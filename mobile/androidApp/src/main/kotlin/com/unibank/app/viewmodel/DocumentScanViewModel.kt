package com.unibank.app.viewmodel

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.unibank.shared.data.local.SessionManager
import com.unibank.shared.data.remote.grpc.AiGrpcClient
import com.unibank.shared.domain.model.BillFields
import com.unibank.shared.domain.model.ChequeFields
import com.unibank.shared.domain.util.Result
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import kotlinx.coroutines.withTimeout

data class DocumentScanUiState(
    val isLoading: Boolean = false,
    val chequeFields: ChequeFields? = null,
    val billFields: BillFields? = null,
    val depositSubmitted: Boolean = false,
    val error: String? = null,
)

class DocumentScanViewModel(
    private val aiClient: AiGrpcClient,
    private val sessionManager: SessionManager,
) : ViewModel() {

    private val _uiState = MutableStateFlow(DocumentScanUiState())
    val uiState = _uiState.asStateFlow()

    fun extractChequeFields(chequeImage: ByteArray) {
        val accountId = sessionManager.getAccountId() ?: return
        viewModelScope.launch {
            _uiState.value = _uiState.value.copy(isLoading = true, error = null, chequeFields = null)
            try {
                val result = withTimeout(60_000L) {
                    aiClient.extractChequeFields(accountId, chequeImage)
                }
                when (result) {
                    is Result.Success -> _uiState.value = _uiState.value.copy(
                        chequeFields = result.data,
                        isLoading = false,
                    )
                    is Result.Failure -> _uiState.value = _uiState.value.copy(
                        error = "Could not read cheque. Please enter details manually.",
                        isLoading = false,
                    )
                }
            } catch (e: Exception) {
                _uiState.value = _uiState.value.copy(
                    error = "Could not read cheque. Please enter details manually.",
                    isLoading = false,
                )
            }
        }
    }

    fun extractBillFields(billImage: ByteArray) {
        val accountId = sessionManager.getAccountId() ?: return
        viewModelScope.launch {
            _uiState.value = _uiState.value.copy(isLoading = true, error = null, billFields = null)
            try {
                val result = withTimeout(60_000L) {
                    aiClient.extractBillFields(accountId, billImage)
                }
                when (result) {
                    is Result.Success -> _uiState.value = _uiState.value.copy(
                        billFields = result.data,
                        isLoading = false,
                    )
                    is Result.Failure -> _uiState.value = _uiState.value.copy(
                        error = "Could not read bill. Please enter details manually.",
                        isLoading = false,
                    )
                }
            } catch (e: Exception) {
                _uiState.value = _uiState.value.copy(
                    error = "Could not read bill. Please enter details manually.",
                    isLoading = false,
                )
            }
        }
    }

    fun submitChequeDeposit(
        chequeNumber: String,
        amount: String,
        payee: String,
        date: String,
        bank: String,
        branchCode: String,
        accountNumber: String,
    ) {
        viewModelScope.launch {
            _uiState.value = _uiState.value.copy(isLoading = true, error = null)
            // TODO: Wire to actual deposit payment flow
            _uiState.value = _uiState.value.copy(depositSubmitted = true, isLoading = false)
        }
    }

    fun reset() {
        _uiState.value = DocumentScanUiState()
    }
}
