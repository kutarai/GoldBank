package com.goldbank.app.viewmodel

import android.os.Build
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.goldbank.shared.data.local.PreferencesManager
import com.goldbank.shared.data.local.SessionManager
import com.goldbank.shared.data.remote.grpc.AccountGrpcClient
import com.goldbank.shared.domain.model.DeviceTransferInitResult
import com.goldbank.shared.domain.model.Profile
import com.goldbank.shared.domain.util.Result
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.launch

sealed interface ProfileUiState {
    data object Idle : ProfileUiState
    data object Loading : ProfileUiState
    data class ProfileLoaded(val profile: Profile) : ProfileUiState
    data class ProfileUpdated(val profile: Profile) : ProfileUiState
    data class DeviceTransferInitiated(val result: DeviceTransferInitResult) : ProfileUiState
    data class DeviceTransferCompleted(val message: String) : ProfileUiState
    data class Error(val message: String) : ProfileUiState
}

class ProfileViewModel(
    private val accountClient: AccountGrpcClient,
    private val sessionManager: SessionManager,
    private val preferencesManager: PreferencesManager,
) : ViewModel() {

    private val _uiState = MutableStateFlow<ProfileUiState>(ProfileUiState.Idle)
    val uiState: StateFlow<ProfileUiState> = _uiState.asStateFlow()

    val isDarkMode = preferencesManager.isDarkMode.stateIn(viewModelScope, SharingStarted.Lazily, false)
    val notificationsEnabled = preferencesManager.notificationsEnabled.stateIn(viewModelScope, SharingStarted.Lazily, true)
    val language = preferencesManager.language.stateIn(viewModelScope, SharingStarted.Lazily, "en")

    fun loadProfile() {
        viewModelScope.launch {
            _uiState.value = ProfileUiState.Loading
            val accountId = sessionManager.getAccountId() ?: ""
            when (val result = accountClient.getProfile(accountId)) {
                is Result.Success -> _uiState.value = ProfileUiState.ProfileLoaded(result.data)
                is Result.Failure -> _uiState.value = ProfileUiState.Error(result.error.toString())
            }
        }
    }

    fun updateProfile(
        firstName: String?,
        lastName: String?,
        email: String?,
        dateOfBirth: String?,
    ) {
        viewModelScope.launch {
            _uiState.value = ProfileUiState.Loading
            val accountId = sessionManager.getAccountId() ?: ""
            when (val result = accountClient.updateProfile(
                accountId = accountId,
                firstName = firstName,
                lastName = lastName,
                email = email,
                dateOfBirth = dateOfBirth,
                nationalId = null,
            )) {
                is Result.Success -> _uiState.value = ProfileUiState.ProfileUpdated(result.data)
                is Result.Failure -> _uiState.value = ProfileUiState.Error(result.error.toString())
            }
        }
    }

    fun initiateDeviceTransfer(phoneNumber: String) {
        viewModelScope.launch {
            _uiState.value = ProfileUiState.Loading
            @Suppress("DEPRECATION")
            val deviceId = Build.SERIAL ?: Build.UNKNOWN
            when (val result = accountClient.initiateDeviceTransfer(phoneNumber, deviceId)) {
                is Result.Success -> _uiState.value = ProfileUiState.DeviceTransferInitiated(result.data)
                is Result.Failure -> _uiState.value = ProfileUiState.Error(result.error.toString())
            }
        }
    }

    fun completeDeviceTransfer(transferReference: String, otp: String, pin: String) {
        viewModelScope.launch {
            _uiState.value = ProfileUiState.Loading
            @Suppress("DEPRECATION")
            val deviceId = Build.SERIAL ?: Build.UNKNOWN
            when (val result = accountClient.completeDeviceTransfer(transferReference, otp, pin, deviceId)) {
                is Result.Success -> {
                    if (result.data.success) {
                        _uiState.value = ProfileUiState.DeviceTransferCompleted(result.data.message)
                    } else {
                        _uiState.value = ProfileUiState.Error(result.data.message)
                    }
                }
                is Result.Failure -> _uiState.value = ProfileUiState.Error(result.error.toString())
            }
        }
    }

    fun setDarkMode(enabled: Boolean) {
        viewModelScope.launch { preferencesManager.setDarkMode(enabled) }
    }

    fun setNotifications(enabled: Boolean) {
        viewModelScope.launch { preferencesManager.setNotificationsEnabled(enabled) }
    }

    fun setLanguage(lang: String) {
        viewModelScope.launch { preferencesManager.setLanguage(lang) }
    }

    fun resetState() {
        _uiState.value = ProfileUiState.Idle
    }
}
