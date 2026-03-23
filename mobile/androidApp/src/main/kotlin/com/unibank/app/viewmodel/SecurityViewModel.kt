package com.unibank.app.viewmodel

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.unibank.shared.data.local.SecurityPreferences
import com.unibank.shared.data.local.SessionManager
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

sealed interface SecurityState {
    data object BiometricRequired : SecurityState
    data object PinRequired : SecurityState
    data object Unlocked : SecurityState
    data object Loading : SecurityState
}

data class SecurityUiState(
    val securityState: SecurityState = SecurityState.Loading,
    val biometricAvailable: Boolean = false,
    val biometricEnabled: Boolean = false,
    val inactivityTimeoutMinutes: Int = 3,
    val biometricFailCount: Int = 0,
    val error: String? = null,
)

class SecurityViewModel(
    private val securityPreferences: SecurityPreferences,
    private val sessionManager: SessionManager,
) : ViewModel() {

    private val _uiState = MutableStateFlow(SecurityUiState())
    val uiState = _uiState.asStateFlow()

    private var inactivityJob: Job? = null

    init {
        loadSettings()
    }

    private fun loadSettings() {
        _uiState.value = _uiState.value.copy(
            biometricEnabled = securityPreferences.biometricEnabled,
            inactivityTimeoutMinutes = securityPreferences.inactivityTimeoutMinutes,
        )
    }

    fun checkLockState(biometricAvailable: Boolean) {
        val state = _uiState.value
        _uiState.value = state.copy(biometricAvailable = biometricAvailable)

        val lastActive = securityPreferences.lastActiveTimestamp
        val timeoutMs = state.inactivityTimeoutMinutes * 60 * 1000L
        val isTimedOut = lastActive > 0 && (System.currentTimeMillis() - lastActive) > timeoutMs

        when {
            isTimedOut && state.biometricEnabled && biometricAvailable ->
                _uiState.value = _uiState.value.copy(securityState = SecurityState.BiometricRequired)
            isTimedOut ->
                _uiState.value = _uiState.value.copy(securityState = SecurityState.PinRequired)
            state.biometricEnabled && biometricAvailable ->
                _uiState.value = _uiState.value.copy(securityState = SecurityState.BiometricRequired)
            else ->
                _uiState.value = _uiState.value.copy(securityState = SecurityState.Unlocked)
        }
    }

    fun onBiometricSuccess() {
        securityPreferences.lastActiveTimestamp = System.currentTimeMillis()
        _uiState.value = _uiState.value.copy(
            securityState = SecurityState.Unlocked,
            biometricFailCount = 0,
        )
        startInactivityTimer()
    }

    fun onBiometricFailed() {
        val newCount = _uiState.value.biometricFailCount + 1
        if (newCount >= 3) {
            _uiState.value = _uiState.value.copy(
                securityState = SecurityState.PinRequired,
                biometricFailCount = newCount,
            )
        } else {
            _uiState.value = _uiState.value.copy(biometricFailCount = newCount)
        }
    }

    fun onPinVerified() {
        securityPreferences.lastActiveTimestamp = System.currentTimeMillis()
        _uiState.value = _uiState.value.copy(
            securityState = SecurityState.Unlocked,
            biometricFailCount = 0,
        )
        startInactivityTimer()
    }

    fun onUserActivity() {
        securityPreferences.lastActiveTimestamp = System.currentTimeMillis()
        startInactivityTimer()
    }

    fun setBiometricEnabled(enabled: Boolean) {
        securityPreferences.biometricEnabled = enabled
        _uiState.value = _uiState.value.copy(biometricEnabled = enabled)
    }

    fun setInactivityTimeout(minutes: Int) {
        securityPreferences.inactivityTimeoutMinutes = minutes
        _uiState.value = _uiState.value.copy(inactivityTimeoutMinutes = minutes)
    }

    private fun startInactivityTimer() {
        inactivityJob?.cancel()
        inactivityJob = viewModelScope.launch {
            val timeoutMs = _uiState.value.inactivityTimeoutMinutes * 60 * 1000L
            delay(timeoutMs)
            _uiState.value = _uiState.value.copy(
                securityState = if (_uiState.value.biometricEnabled && _uiState.value.biometricAvailable)
                    SecurityState.BiometricRequired
                else SecurityState.PinRequired,
            )
        }
    }
}
