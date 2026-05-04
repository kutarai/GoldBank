package com.goldbank.shared.domain.usecase.auth

import com.goldbank.shared.domain.model.AuthResult
import com.goldbank.shared.domain.repository.AuthRepository
import com.goldbank.shared.domain.util.Result

class LoginUseCase(private val authRepository: AuthRepository) {
    suspend operator fun invoke(
        phoneNumber: String,
        pin: String,
        deviceId: String,
        tenantId: String,
    ): Result<AuthResult> =
        authRepository.authenticate(phoneNumber, pin, deviceId, tenantId)
}
