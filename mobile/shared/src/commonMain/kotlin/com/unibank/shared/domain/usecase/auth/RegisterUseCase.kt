package com.unibank.shared.domain.usecase.auth

import com.unibank.shared.domain.model.RegistrationResult
import com.unibank.shared.domain.repository.AuthRepository
import com.unibank.shared.domain.util.Result

class RegisterUseCase(private val authRepository: AuthRepository) {
    suspend operator fun invoke(
        phoneNumber: String,
        deviceId: String,
        tenantId: String,
    ): Result<RegistrationResult> =
        authRepository.register(phoneNumber, deviceId, tenantId)
}
