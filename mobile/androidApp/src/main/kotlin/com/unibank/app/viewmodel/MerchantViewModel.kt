package com.unibank.app.viewmodel

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.unibank.shared.data.local.SessionManager
import com.unibank.shared.data.remote.grpc.MerchantGrpcClient
import com.unibank.shared.domain.model.*
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.toList
import kotlinx.coroutines.launch
import java.time.LocalDate

sealed interface MerchantUiState {
    data object Idle : MerchantUiState
    data object Loading : MerchantUiState
    data class Registered(val result: MerchantRegistrationResult) : MerchantUiState
    data class ProfileLoaded(val profile: MerchantProfile) : MerchantUiState
    data class StatusLoaded(val status: MerchantStatusInfo) : MerchantUiState
    data class TransactionsLoaded(val transactions: List<MerchantTransaction>) : MerchantUiState
    data class SettlementsLoaded(val settlements: List<Settlement>) : MerchantUiState
    data class SettlementDetailLoaded(val detail: SettlementDetail) : MerchantUiState
    data class CommissionLoaded(val report: MerchantCommissionReport) : MerchantUiState
    data class DocumentUploaded(val documentId: String, val message: String) : MerchantUiState
    data class Error(val message: String) : MerchantUiState
}

class MerchantViewModel(
    private val merchantClient: MerchantGrpcClient,
    private val sessionManager: SessionManager,
) : ViewModel() {

    private val _uiState = MutableStateFlow<MerchantUiState>(MerchantUiState.Idle)
    val uiState: StateFlow<MerchantUiState> = _uiState.asStateFlow()

    private var currentMerchantId: String? = null

    fun register(
        businessName: String,
        businessType: String,
        registrationNumber: String,
        taxId: String,
        categoryCode: String,
        address: MerchantAddress,
        isAgent: Boolean,
    ) {
        viewModelScope.launch {
            _uiState.value = MerchantUiState.Loading
            try {
                val accountId = sessionManager.getAccountId() ?: ""
                val result = merchantClient.register(
                    accountId = accountId,
                    businessName = businessName,
                    businessType = businessType,
                    registrationNumber = registrationNumber,
                    taxId = taxId,
                    categoryCode = categoryCode,
                    address = address,
                    isAgent = isAgent,
                )
                currentMerchantId = result.merchantId
                _uiState.value = MerchantUiState.Registered(result)
            } catch (e: Exception) {
                _uiState.value = MerchantUiState.Error(e.message ?: "Registration failed")
            }
        }
    }

    fun loadProfile(merchantId: String? = null) {
        viewModelScope.launch {
            _uiState.value = MerchantUiState.Loading
            try {
                val id = merchantId ?: currentMerchantId ?: ""
                val profile = merchantClient.getProfile(id)
                currentMerchantId = profile.merchantId
                _uiState.value = MerchantUiState.ProfileLoaded(profile)
            } catch (e: Exception) {
                _uiState.value = MerchantUiState.Error(e.message ?: "Failed to load profile")
            }
        }
    }

    fun loadStatus(merchantId: String? = null) {
        viewModelScope.launch {
            _uiState.value = MerchantUiState.Loading
            try {
                val id = merchantId ?: currentMerchantId ?: ""
                val status = merchantClient.getMerchantStatus(id)
                currentMerchantId = status.merchantId
                _uiState.value = MerchantUiState.StatusLoaded(status)
            } catch (e: Exception) {
                _uiState.value = MerchantUiState.Error(e.message ?: "Failed to load status")
            }
        }
    }

    fun loadTransactions(merchantId: String? = null, days: Int = 30) {
        viewModelScope.launch {
            _uiState.value = MerchantUiState.Loading
            try {
                val id = merchantId ?: currentMerchantId ?: ""
                val end = LocalDate.now()
                val start = end.minusDays(days.toLong())
                val txns = merchantClient.getTransactions(
                    merchantId = id,
                    startDate = start.toString(),
                    endDate = end.toString(),
                ).toList()
                _uiState.value = MerchantUiState.TransactionsLoaded(txns)
            } catch (e: Exception) {
                _uiState.value = MerchantUiState.Error(e.message ?: "Failed to load transactions")
            }
        }
    }

    fun loadSettlements(merchantId: String? = null, days: Int = 90) {
        viewModelScope.launch {
            _uiState.value = MerchantUiState.Loading
            try {
                val id = merchantId ?: currentMerchantId ?: ""
                val end = LocalDate.now()
                val start = end.minusDays(days.toLong())
                val settlements = merchantClient.getSettlements(
                    merchantId = id,
                    startDate = start.toString(),
                    endDate = end.toString(),
                )
                _uiState.value = MerchantUiState.SettlementsLoaded(settlements)
            } catch (e: Exception) {
                _uiState.value = MerchantUiState.Error(e.message ?: "Failed to load settlements")
            }
        }
    }

    fun loadCommissionReport(merchantId: String? = null, days: Int = 30) {
        viewModelScope.launch {
            _uiState.value = MerchantUiState.Loading
            try {
                val id = merchantId ?: currentMerchantId ?: ""
                val end = LocalDate.now()
                val start = end.minusDays(days.toLong())
                val report = merchantClient.getCommissionReport(
                    merchantId = id,
                    startDate = start.toString(),
                    endDate = end.toString(),
                )
                _uiState.value = MerchantUiState.CommissionLoaded(report)
            } catch (e: Exception) {
                _uiState.value = MerchantUiState.Error(e.message ?: "Failed to load commission report")
            }
        }
    }

    fun uploadDocument(
        merchantId: String? = null,
        documentType: String,
        fileName: String,
        contentType: String,
        fileBytes: ByteArray,
    ) {
        viewModelScope.launch {
            _uiState.value = MerchantUiState.Loading
            try {
                val id = merchantId ?: currentMerchantId ?: ""
                val response = merchantClient.uploadBusinessDocument(
                    merchantId = id,
                    documentType = documentType,
                    fileName = fileName,
                    contentType = contentType,
                    fileBytes = fileBytes,
                )
                _uiState.value = MerchantUiState.DocumentUploaded(response.documentId, response.message)
            } catch (e: Exception) {
                _uiState.value = MerchantUiState.Error(e.message ?: "Upload failed")
            }
        }
    }

    fun resetState() {
        _uiState.value = MerchantUiState.Idle
    }
}
