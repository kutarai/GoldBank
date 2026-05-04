package com.goldbank.shared.domain.repository

import com.goldbank.shared.domain.model.AuthResult
import com.goldbank.shared.domain.model.AuthTokens
import com.goldbank.shared.domain.model.OtpVerificationResult
import com.goldbank.shared.domain.model.RegistrationResult
import com.goldbank.shared.domain.model.SessionState
import com.goldbank.shared.domain.util.Result
import kotlinx.coroutines.flow.Flow

interface AuthRepository {
    suspend fun register(phoneNumber: String, deviceId: String, tenantId: String): Result<RegistrationResult>
    suspend fun verifyOtp(registrationId: String, otp: String, phoneNumber: String): Result<OtpVerificationResult>
    suspend fun createPin(accountId: String, pin: String, pinConfirmation: String): Result<AuthTokens>
    suspend fun authenticate(phoneNumber: String, pin: String, deviceId: String, tenantId: String): Result<AuthResult>
    suspend fun refreshToken(refreshToken: String, deviceId: String): Result<AuthTokens>
    suspend fun logout(accountId: String, allDevices: Boolean): Result<Unit>
    fun observeSessionState(): Flow<SessionState>
}
