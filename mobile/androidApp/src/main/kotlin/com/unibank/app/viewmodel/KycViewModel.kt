package com.unibank.app.viewmodel

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.unibank.shared.data.local.SessionManager
import com.unibank.shared.data.remote.grpc.KycGrpcClient
import com.unibank.shared.domain.model.KycStatus
import com.unibank.shared.domain.model.SelfieUploadResult
import com.unibank.shared.domain.model.UploadResult
import com.unibank.shared.domain.util.Result
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

sealed interface KycUiState {
    data object Idle : KycUiState
    data object Loading : KycUiState
    data class StatusLoaded(val status: KycStatus) : KycUiState
    data class DocumentUploaded(val result: UploadResult) : KycUiState
    data class SelfieUploaded(val result: SelfieUploadResult) : KycUiState
    data class Error(val message: String) : KycUiState
}

class KycViewModel(
    private val kycClient: KycGrpcClient,
    private val sessionManager: SessionManager,
) : ViewModel() {

    private val _uiState = MutableStateFlow<KycUiState>(KycUiState.Idle)
    val uiState: StateFlow<KycUiState> = _uiState.asStateFlow()

    private val _kycStatus = MutableStateFlow<KycStatus?>(null)
    val kycStatus: StateFlow<KycStatus?> = _kycStatus.asStateFlow()

    fun loadKycStatus() {
        viewModelScope.launch {
            _uiState.value = KycUiState.Loading
            val accountId = sessionManager.getAccountId() ?: return@launch
            when (val result = kycClient.getKycStatus(accountId)) {
                is Result.Success -> {
                    _kycStatus.value = result.data
                    _uiState.value = KycUiState.StatusLoaded(result.data)
                }
                is Result.Failure -> {
                    _uiState.value = KycUiState.Error(result.error.message())
                }
            }
        }
    }

    fun uploadDocument(
        documentType: String,
        fileName: String,
        contentType: String,
        fileBytes: ByteArray,
    ) {
        viewModelScope.launch {
            _uiState.value = KycUiState.Loading
            val accountId = sessionManager.getAccountId() ?: return@launch
            when (val result = kycClient.uploadDocument(accountId, documentType, fileName, contentType, fileBytes)) {
                is Result.Success -> _uiState.value = KycUiState.DocumentUploaded(result.data)
                is Result.Failure -> _uiState.value = KycUiState.Error(result.error.message())
            }
        }
    }

    fun uploadSelfie(
        contentType: String,
        fileBytes: ByteArray,
    ) {
        viewModelScope.launch {
            _uiState.value = KycUiState.Loading
            val accountId = sessionManager.getAccountId() ?: return@launch
            when (val result = kycClient.uploadSelfie(accountId, contentType, fileBytes)) {
                is Result.Success -> _uiState.value = KycUiState.SelfieUploaded(result.data)
                is Result.Failure -> _uiState.value = KycUiState.Error(result.error.message())
            }
        }
    }

    fun resetState() {
        _uiState.value = KycUiState.Idle
    }

    private fun com.unibank.shared.domain.util.AppError.message(): String = when (this) {
        is com.unibank.shared.domain.util.AppError.Network -> "Network error. Check your connection."
        is com.unibank.shared.domain.util.AppError.Server -> message
        is com.unibank.shared.domain.util.AppError.Unauthenticated -> message
        is com.unibank.shared.domain.util.AppError.Validation -> "$field: $message"
        is com.unibank.shared.domain.util.AppError.Unknown -> throwable.localizedMessage ?: "Unknown error"
    }
}
