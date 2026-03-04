package com.unibank.shared.domain.repository

import com.unibank.shared.domain.model.AuthResult
import com.unibank.shared.domain.model.AuthTokens
import com.unibank.shared.domain.model.OtpVerificationResult
import com.unibank.shared.domain.model.RegistrationResult
import com.unibank.shared.domain.model.SessionState
import com.unibank.shared.domain.util.Result
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
