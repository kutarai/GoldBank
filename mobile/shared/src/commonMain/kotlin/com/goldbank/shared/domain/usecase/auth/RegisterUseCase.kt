package com.goldbank.shared.domain.usecase.auth

import com.goldbank.shared.domain.model.RegistrationResult
import com.goldbank.shared.domain.repository.AuthRepository
import com.goldbank.shared.domain.util.Result

class RegisterUseCase(private val authRepository: AuthRepository) {
    suspend operator fun invoke(
        phoneNumber: String,
        deviceId: String,
        tenantId: String,
    ): Result<RegistrationResult> =
        authRepository.register(phoneNumber, deviceId, tenantId)
}
