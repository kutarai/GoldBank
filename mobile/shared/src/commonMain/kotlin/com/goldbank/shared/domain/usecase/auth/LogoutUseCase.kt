package com.goldbank.shared.domain.usecase.auth

import com.goldbank.shared.domain.repository.AuthRepository
import com.goldbank.shared.domain.util.Result

class LogoutUseCase(private val authRepository: AuthRepository) {
    suspend operator fun invoke(accountId: String, allDevices: Boolean = false): Result<Unit> =
        authRepository.logout(accountId, allDevices)
}
