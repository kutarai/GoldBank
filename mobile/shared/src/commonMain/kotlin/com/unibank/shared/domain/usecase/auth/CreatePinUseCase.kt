package com.unibank.shared.domain.usecase.auth

import com.unibank.shared.domain.model.AuthTokens
import com.unibank.shared.domain.repository.AuthRepository
import com.unibank.shared.domain.util.Result

class CreatePinUseCase(private val authRepository: AuthRepository) {
    suspend operator fun invoke(
        accountId: String,
        pin: String,
        pinConfirmation: String,
    ): Result<AuthTokens> =
        authRepository.createPin(accountId, pin, pinConfirmation)
}
