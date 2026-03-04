package com.unibank.shared.domain.usecase.auth

import com.unibank.shared.domain.model.AuthResult
import com.unibank.shared.domain.repository.AuthRepository
import com.unibank.shared.domain.util.Result

class LoginUseCase(private val authRepository: AuthRepository) {
    suspend operator fun invoke(
        phoneNumber: String,
        pin: String,
        deviceId: String,
        tenantId: String,
    ): Result<AuthResult> =
        authRepository.authenticate(phoneNumber, pin, deviceId, tenantId)
}
