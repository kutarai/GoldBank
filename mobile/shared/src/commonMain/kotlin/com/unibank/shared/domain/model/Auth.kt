package com.unibank.shared.domain.model

data class AuthTokens(
    val accessToken: String,
    val refreshToken: String,
    val accessTokenExpiresIn: Long,
    val refreshTokenExpiresIn: Long,
    val accountId: String,
)

data class RegistrationResult(
    val success: Boolean,
    val message: String,
    val registrationId: String,
    val otpLength: Int,
    val otpTtlSeconds: Int,
)

data class OtpVerificationResult(
    val success: Boolean,
    val message: String,
    val accountId: String,
    val temporaryToken: String,
)

sealed interface AuthResult {
    data class Success(val tokens: AuthTokens) : AuthResult
    data class LockedOut(val remainingSeconds: Long) : AuthResult
    data class Failed(val message: String, val remainingAttempts: Int) : AuthResult
}

sealed interface SessionState {
    data object Loading : SessionState
    data object Unauthenticated : SessionState
    data class Authenticated(val accountId: String) : SessionState
    data object PinRequired : SessionState
}
