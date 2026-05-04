package com.goldbank.shared.domain.usecase.auth

import com.goldbank.shared.domain.model.AuthTokens
import com.goldbank.shared.domain.repository.AuthRepository
import com.goldbank.shared.domain.util.Result

class CreatePinUseCase(private val authRepository: AuthRepository) {
    suspend operator fun invoke(
        accountId: String,
        pin: String,
        pinConfirmation: String,
    ): Result<AuthTokens> =
        authRepository.createPin(accountId, pin, pinConfirmation)
}
