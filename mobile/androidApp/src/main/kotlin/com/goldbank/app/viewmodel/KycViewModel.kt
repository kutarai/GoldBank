package com.goldbank.app.viewmodel

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.goldbank.shared.data.local.SessionManager
import com.goldbank.shared.data.remote.grpc.AiGrpcClient
import com.goldbank.shared.data.remote.grpc.KycGrpcClient
import com.goldbank.shared.domain.model.KycStatus
import com.goldbank.shared.domain.model.KycVerificationResult
import com.goldbank.shared.domain.model.SelfieUploadResult
import com.goldbank.shared.domain.model.UploadResult
import com.goldbank.shared.domain.util.Result
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import kotlinx.coroutines.withTimeout

sealed interface KycUiState {
    data object Idle : KycUiState
    data object Loading : KycUiState
    data class StatusLoaded(val status: KycStatus) : KycUiState
    data class DocumentUploaded(val result: UploadResult) : KycUiState
    data class SelfieUploaded(val result: SelfieUploadResult) : KycUiState
    data class Error(val message: String) : KycUiState
}

data class KycVerificationUiState(
    val verificationResult: KycVerificationResult? = null,
    val proofOfAddressResult: KycVerificationResult? = null,
    val isVerifying: Boolean = false,
    val verificationError: String? = null,
)

class KycViewModel(
    private val kycClient: KycGrpcClient,
    private val sessionManager: SessionManager,
    private val aiClient: AiGrpcClient,
) : ViewModel() {

    private val _uiState = MutableStateFlow<KycUiState>(KycUiState.Idle)
    val uiState: StateFlow<KycUiState> = _uiState.asStateFlow()

    private val _kycStatus = MutableStateFlow<KycStatus?>(null)
    val kycStatus: StateFlow<KycStatus?> = _kycStatus.asStateFlow()

    private val _verificationUiState = MutableStateFlow(KycVerificationUiState())
    val verificationUiState: StateFlow<KycVerificationUiState> = _verificationUiState.asStateFlow()

    private var capturedSelfieBytes: ByteArray? = null
    private var capturedIdDocumentBytes: ByteArray? = null

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

    fun setSelfieBytes(bytes: ByteArray) { capturedSelfieBytes = bytes }
    fun setIdDocumentBytes(bytes: ByteArray) { capturedIdDocumentBytes = bytes }

    fun verifyIdentity() {
        val selfie = capturedSelfieBytes ?: return
        val idDoc = capturedIdDocumentBytes ?: return
        viewModelScope.launch {
            _verificationUiState.value = _verificationUiState.value.copy(isVerifying = true, verificationError = null)
            try {
                val result = withTimeout(120_000L) {
                    aiClient.verifyIdentity(sessionManager.getAccountId() ?: "", selfie, idDoc)
                }
                when (result) {
                    is Result.Success -> _verificationUiState.value = _verificationUiState.value.copy(
                        verificationResult = result.data,
                        isVerifying = false,
                    )
                    is Result.Failure -> _verificationUiState.value = _verificationUiState.value.copy(
                        verificationError = "Verification service unavailable. Your documents have been saved — we'll verify shortly.",
                        isVerifying = false,
                    )
                }
            } catch (e: Exception) {
                _verificationUiState.value = _verificationUiState.value.copy(
                    verificationError = "Verification service unavailable. Your documents have been saved — we'll verify shortly.",
                    isVerifying = false,
                )
            }
        }
    }

    fun verifyProofOfAddress(documentBytes: ByteArray) {
        viewModelScope.launch {
            _verificationUiState.value = _verificationUiState.value.copy(isVerifying = true, verificationError = null)
            try {
                val result = withTimeout(120_000L) {
                    aiClient.verifyProofOfAddress(sessionManager.getAccountId() ?: "", documentBytes)
                }
                when (result) {
                    is Result.Success -> _verificationUiState.value = _verificationUiState.value.copy(
                        proofOfAddressResult = result.data,
                        isVerifying = false,
                    )
                    is Result.Failure -> _verificationUiState.value = _verificationUiState.value.copy(
                        verificationError = "Verification failed. Please try again.",
                        isVerifying = false,
                    )
                }
            } catch (e: Exception) {
                _verificationUiState.value = _verificationUiState.value.copy(
                    verificationError = "Verification failed. Please try again.",
                    isVerifying = false,
                )
            }
        }
    }

    private fun com.goldbank.shared.domain.util.AppError.message(): String = when (this) {
        is com.goldbank.shared.domain.util.AppError.Network -> "Network error. Check your connection."
        is com.goldbank.shared.domain.util.AppError.Server -> message
        is com.goldbank.shared.domain.util.AppError.Unauthenticated -> message
        is com.goldbank.shared.domain.util.AppError.Validation -> "$field: $message"
        is com.goldbank.shared.domain.util.AppError.Unknown -> throwable.localizedMessage ?: "Unknown error"
    }
}
