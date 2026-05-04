package com.goldbank.shared.domain.usecase.account

import com.goldbank.shared.domain.model.Profile
import com.goldbank.shared.domain.repository.AccountRepository
import com.goldbank.shared.domain.util.Result

class GetProfileUseCase(private val accountRepository: AccountRepository) {
    suspend operator fun invoke(accountId: String): Result<Profile> =
        accountRepository.getProfile(accountId)
}
