package com.unibank.shared.domain.usecase.account

import com.unibank.shared.domain.model.Profile
import com.unibank.shared.domain.repository.AccountRepository
import com.unibank.shared.domain.util.Result

class GetProfileUseCase(private val accountRepository: AccountRepository) {
    suspend operator fun invoke(accountId: String): Result<Profile> =
        accountRepository.getProfile(accountId)
}
