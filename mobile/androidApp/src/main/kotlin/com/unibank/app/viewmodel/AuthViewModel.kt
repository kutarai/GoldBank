package com.unibank.app.viewmodel

import android.provider.Settings
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.unibank.shared.data.local.SessionManager
import com.unibank.shared.data.remote.grpc.AccountGrpcClient
import com.unibank.shared.data.remote.grpc.KycGrpcClient
import com.unibank.shared.domain.model.AuthResult
import com.unibank.shared.domain.model.OtpVerificationResult
import com.unibank.shared.domain.model.RegistrationResult
import com.unibank.shared.domain.usecase.auth.CreatePinUseCase
import com.unibank.shared.domain.usecase.auth.LoginUseCase
import com.unibank.shared.domain.usecase.auth.LogoutUseCase
import com.unibank.shared.domain.usecase.auth.RegisterUseCase
import com.unibank.shared.domain.usecase.auth.VerifyOtpUseCase
import com.unibank.shared.domain.util.AppError
import com.unibank.shared.domain.util.Result
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

class AuthViewModel(
    private val registerUseCase: RegisterUseCase,
    private val verifyOtpUseCase: VerifyOtpUseCase,
    private val createPinUseCase: CreatePinUseCase,
    private val loginUseCase: LoginUseCase,
    private val logoutUseCase: LogoutUseCase,
    private val sessionManager: SessionManager,
    private val accountClient: AccountGrpcClient,
    private val kycClient: KycGrpcClient,
) : ViewModel() {

    private val _uiState = MutableStateFlow<AuthUiState>(AuthUiState.Idle)
    val uiState: StateFlow<AuthUiState> = _uiState.asStateFlow()

    // Persisted across navigation within auth flow
    private var currentPhoneNumber: String = ""
    private var currentRegistrationId: String = ""
    private var currentOtpLength: Int = 6
    private var currentOtpTtlSeconds: Int = 120
    private var currentAccountId: String = ""
    private var currentTemporaryToken: String = ""

    fun register(phoneNumber: String, deviceId: String) {
        currentPhoneNumber = phoneNumber
        _uiState.value = AuthUiState.Loading
        viewModelScope.launch {
            when (val result = registerUseCase(phoneNumber, deviceId, sessionManager.currentTenantId)) {
                is Result.Success -> {
                    val data = result.data
                    if (data.success) {
                        currentRegistrationId = data.registrationId
                        currentOtpLength = data.otpLength
                        currentOtpTtlSeconds = data.otpTtlSeconds
                        _uiState.value = AuthUiState.OtpSent(
                            registrationId = data.registrationId,
                            otpLength = data.otpLength,
                            ttlSeconds = data.otpTtlSeconds,
                            message = data.message,
                        )
                    } else {
                        _uiState.value = AuthUiState.Error(data.message)
                    }
                }
                is Result.Failure -> _uiState.value = AuthUiState.Error(result.error.displayMessage())
            }
        }
    }

    fun verifyOtp(otp: String) {
        _uiState.value = AuthUiState.Loading
        viewModelScope.launch {
            when (val result = verifyOtpUseCase(currentRegistrationId, otp, currentPhoneNumber)) {
                is Result.Success -> {
                    val data = result.data
                    if (data.success) {
                        currentAccountId = data.accountId
                        currentTemporaryToken = data.temporaryToken
                        _uiState.value = AuthUiState.OtpVerified(
                            accountId = data.accountId,
                        )
                    } else {
                        _uiState.value = AuthUiState.Error(data.message)
                    }
                }
                is Result.Failure -> _uiState.value = AuthUiState.Error(result.error.displayMessage())
            }
        }
    }

    fun createPin(pin: String, pinConfirmation: String) {
        _uiState.value = AuthUiState.Loading
        viewModelScope.launch {
            when (val result = createPinUseCase(currentAccountId, pin, pinConfirmation)) {
                is Result.Success -> {
                    _uiState.value = AuthUiState.PinCreated(currentAccountId)
                }
                is Result.Failure -> _uiState.value = AuthUiState.Error(result.error.displayMessage())
            }
        }
    }

    fun updateRegistrationProfile(
        firstName: String,
        lastName: String,
        nationalId: String,
        dateOfBirth: String,
    ) {
        _uiState.value = AuthUiState.Loading
        viewModelScope.launch {
            val accountId = sessionManager.getAccountId() ?: return@launch
            when (val result = accountClient.updateProfile(
                accountId = accountId,
                firstName = firstName,
                lastName = lastName,
                email = null,
                dateOfBirth = dateOfBirth,
                nationalId = nationalId,
            )) {
                is Result.Success -> _uiState.value = AuthUiState.ProfileUpdated
                is Result.Failure -> _uiState.value = AuthUiState.Error(result.error.displayMessage())
            }
        }
    }

    fun uploadRegistrationId(contentType: String, fileBytes: ByteArray) {
        _uiState.value = AuthUiState.Loading
        viewModelScope.launch {
            val accountId = sessionManager.getAccountId() ?: return@launch
            when (val result = kycClient.uploadDocument(
                accountId = accountId,
                documentType = "national_id",
                fileName = "national_id.jpg",
                contentType = contentType,
                fileBytes = fileBytes,
            )) {
                is Result.Success -> _uiState.value = AuthUiState.IdUploaded
                is Result.Failure -> _uiState.value = AuthUiState.Error(result.error.displayMessage())
            }
        }
    }

    fun uploadRegistrationSelfie(contentType: String, fileBytes: ByteArray) {
        _uiState.value = AuthUiState.Loading
        viewModelScope.launch {
            val accountId = sessionManager.getAccountId() ?: return@launch
            when (val result = kycClient.uploadSelfie(accountId, contentType, fileBytes)) {
                is Result.Success -> {
                    sessionManager.logout()
                    _uiState.value = AuthUiState.RegistrationComplete
                }
                is Result.Failure -> _uiState.value = AuthUiState.Error(result.error.displayMessage())
            }
        }
    }

    fun login(phoneNumber: String, pin: String, deviceId: String) {
        _uiState.value = AuthUiState.Loading
        viewModelScope.launch {
            when (val result = loginUseCase(phoneNumber, pin, deviceId, sessionManager.currentTenantId)) {
                is Result.Success -> {
                    when (val authResult = result.data) {
                        is AuthResult.Success -> {
                            _uiState.value = AuthUiState.Authenticated
                        }
                        is AuthResult.LockedOut -> {
                            _uiState.value = AuthUiState.LockedOut(authResult.remainingSeconds)
                        }
                        is AuthResult.Failed -> {
                            _uiState.value = AuthUiState.LoginFailed(
                                message = authResult.message,
                                remainingAttempts = authResult.remainingAttempts,
                            )
                        }
                    }
                }
                is Result.Failure -> _uiState.value = AuthUiState.Error(result.error.displayMessage())
            }
        }
    }

    fun resetState() {
        _uiState.value = AuthUiState.Idle
    }

    fun getStoredPhoneNumber(): String? =
        sessionManager.sessionState.value.let {
            // If PinRequired, phone is stored
            currentPhoneNumber.ifEmpty { null }
        }

    private fun AppError.displayMessage(): String = when (this) {
        is AppError.Network -> "Network error. Please check your connection."
        is AppError.Unauthenticated -> message
        is AppError.Server -> message
        is AppError.Validation -> message
        is AppError.Unknown -> message
    }
}

sealed interface AuthUiState {
    data object Idle : AuthUiState
    data object Loading : AuthUiState
    data class OtpSent(
        val registrationId: String,
        val otpLength: Int,
        val ttlSeconds: Int,
        val message: String,
    ) : AuthUiState
    data class OtpVerified(val accountId: String) : AuthUiState
    data class PinCreated(val accountId: String) : AuthUiState
    data object ProfileUpdated : AuthUiState
    data object IdUploaded : AuthUiState
    data object RegistrationComplete : AuthUiState
    data object Authenticated : AuthUiState
    data class LockedOut(val remainingSeconds: Long) : AuthUiState
    data class LoginFailed(val message: String, val remainingAttempts: Int) : AuthUiState
    data class Error(val message: String) : AuthUiState
}
