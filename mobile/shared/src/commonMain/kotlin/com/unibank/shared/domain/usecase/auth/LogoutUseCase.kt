package com.unibank.shared.domain.usecase.auth

import com.unibank.shared.domain.repository.AuthRepository
import com.unibank.shared.domain.util.Result

class LogoutUseCase(private val authRepository: AuthRepository) {
    suspend operator fun invoke(accountId: String, allDevices: Boolean = false): Result<Unit> =
        authRepository.logout(accountId, allDevices)
}
