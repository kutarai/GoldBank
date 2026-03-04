package com.unibank.shared.data.repository

import com.unibank.shared.data.local.SessionManager
import com.unibank.shared.data.remote.grpc.AccountGrpcClient
import com.unibank.shared.domain.model.AuthResult
import com.unibank.shared.domain.model.AuthTokens
import com.unibank.shared.domain.model.OtpVerificationResult
import com.unibank.shared.domain.model.RegistrationResult
import com.unibank.shared.domain.model.SessionState
import com.unibank.shared.domain.repository.AuthRepository
import com.unibank.shared.domain.util.Result
import com.unibank.shared.domain.util.map
import kotlinx.coroutines.flow.Flow

class AuthRepositoryImpl(
    private val accountClient: AccountGrpcClient,
    private val sessionManager: SessionManager,
) : AuthRepository {

    override suspend fun register(
        phoneNumber: String,
        deviceId: String,
        tenantId: String,
    ): Result<RegistrationResult> {
        val result = accountClient.register(phoneNumber, deviceId, tenantId)
        if (result is Result.Success) {
            sessionManager.savePhoneNumber(phoneNumber)
        }
        return result
    }

    override suspend fun verifyOtp(
        registrationId: String,
        otp: String,
        phoneNumber: String,
    ): Result<OtpVerificationResult> {
        val result = accountClient.verifyOtp(registrationId, otp, phoneNumber)
        if (result is Result.Success && result.data.success) {
            sessionManager.saveTemporaryToken(result.data.temporaryToken, result.data.accountId)
        }
        return result
    }

    override suspend fun createPin(
        accountId: String,
        pin: String,
        pinConfirmation: String,
    ): Result<AuthTokens> {
        val result = accountClient.createPin(accountId, pin, pinConfirmation)
        if (result is Result.Success) {
            sessionManager.saveTokensForRegistration(result.data)
        }
        return result
    }

    override suspend fun authenticate(
        phoneNumber: String,
        pin: String,
        deviceId: String,
        tenantId: String,
    ): Result<AuthResult> {
        val result = accountClient.authenticate(phoneNumber, pin, deviceId, tenantId)
        if (result is Result.Success) {
            val authResult = result.data
            if (authResult is AuthResult.Success) {
                sessionManager.saveTokens(authResult.tokens)
                sessionManager.savePhoneNumber(phoneNumber)
            }
        }
        return result
    }

    override suspend fun refreshToken(
        refreshToken: String,
        deviceId: String,
    ): Result<AuthTokens> {
        val accountId = sessionManager.getAccountId() ?: return Result.Failure(
            com.unibank.shared.domain.util.AppError.Unauthenticated("No account ID found")
        )
        val result = accountClient.refreshToken(refreshToken, deviceId, accountId)
        if (result is Result.Success) {
            sessionManager.saveTokens(result.data)
        }
        return result
    }

    override suspend fun logout(accountId: String, allDevices: Boolean): Result<Unit> {
        val result = accountClient.logout(accountId, allDevices)
        if (result is Result.Success) {
            if (allDevices) {
                sessionManager.fullLogout()
            } else {
                sessionManager.logout()
            }
        }
        return result
    }

    override fun observeSessionState(): Flow<SessionState> =
        sessionManager.sessionState
}
