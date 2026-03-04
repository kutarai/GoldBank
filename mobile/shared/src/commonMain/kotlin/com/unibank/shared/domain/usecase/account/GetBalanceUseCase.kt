package com.unibank.shared.domain.usecase.account

import com.unibank.shared.domain.model.Balance
import com.unibank.shared.domain.repository.AccountRepository
import com.unibank.shared.domain.util.Result

class GetBalanceUseCase(private val accountRepository: AccountRepository) {
    suspend operator fun invoke(accountId: String): Result<Balance> =
        accountRepository.getBalance(accountId)
}
