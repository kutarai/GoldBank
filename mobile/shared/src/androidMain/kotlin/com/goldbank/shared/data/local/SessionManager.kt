package com.goldbank.shared.data.local

import com.goldbank.shared.domain.model.AuthTokens
import com.goldbank.shared.domain.model.SessionState
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow

class SessionManager(
    private val secureStorage: SecureStorage,
    private val tenantId: String,
) {
    private val _sessionState = MutableStateFlow<SessionState>(SessionState.Loading)
    val sessionState: StateFlow<SessionState> = _sessionState.asStateFlow()

    val currentTenantId: String get() = tenantId

    init {
        restoreSession()
    }

    private fun restoreSession() {
        val token = secureStorage.accessToken
        val accountId = secureStorage.accountId
        val expiresAt = secureStorage.accessTokenExpiresAt

        if (token != null && accountId != null && expiresAt > System.currentTimeMillis()) {
            _sessionState.value = SessionState.Authenticated(accountId)
        } else if (secureStorage.phoneNumber != null) {
            _sessionState.value = SessionState.PinRequired
        } else {
            _sessionState.value = SessionState.Unauthenticated
        }
    }

    fun saveTokens(tokens: AuthTokens) {
        secureStorage.accessToken = tokens.accessToken
        secureStorage.refreshToken = tokens.refreshToken
        secureStorage.accountId = tokens.accountId
        if (tokens.customerId.isNotBlank()) {
            secureStorage.customerId = tokens.customerId
        }
        secureStorage.accessTokenExpiresAt =
            System.currentTimeMillis() + (tokens.accessTokenExpiresIn * 1000)
        _sessionState.value = SessionState.Authenticated(tokens.accountId)
    }

    fun saveTemporaryToken(token: String, accountId: String) {
        secureStorage.accessToken = token
        secureStorage.accountId = accountId
        // Don't change session state — user still needs to create PIN
    }

    fun saveTokensForRegistration(tokens: AuthTokens) {
        secureStorage.accessToken = tokens.accessToken
        secureStorage.refreshToken = tokens.refreshToken
        secureStorage.accountId = tokens.accountId
        if (tokens.customerId.isNotBlank()) {
            secureStorage.customerId = tokens.customerId
        }
        secureStorage.accessTokenExpiresAt =
            System.currentTimeMillis() + (tokens.accessTokenExpiresIn * 1000)
        // Don't change session state — user still needs to complete profile + selfie
    }

    fun completeRegistration() {
        val accountId = secureStorage.accountId ?: return
        _sessionState.value = SessionState.Authenticated(accountId)
    }

    fun getAccessToken(): String? = secureStorage.accessToken

    fun getRefreshToken(): String? = secureStorage.refreshToken

    fun getAccountId(): String? = secureStorage.accountId

    fun getCustomerId(): String? = secureStorage.customerId

    fun getNfcToken(): String? = secureStorage.nfcToken

    fun saveNfcToken(token: String) {
        secureStorage.nfcToken = token
    }

    fun isTokenExpired(): Boolean {
        return System.currentTimeMillis() >= secureStorage.accessTokenExpiresAt
    }

    fun isTokenExpiringSoon(thresholdMs: Long = 60_000): Boolean {
        return System.currentTimeMillis() >= (secureStorage.accessTokenExpiresAt - thresholdMs)
    }

    fun logout() {
        val phone = secureStorage.phoneNumber
        val customerId = secureStorage.customerId
        secureStorage.clear()
        secureStorage.phoneNumber = phone
        secureStorage.customerId = customerId
        _sessionState.value = SessionState.PinRequired
    }

    fun fullLogout() {
        secureStorage.clear()
        _sessionState.value = SessionState.Unauthenticated
    }

    fun savePhoneNumber(phone: String) {
        secureStorage.phoneNumber = phone
    }

    fun getPhoneNumber(): String? = secureStorage.phoneNumber
}
