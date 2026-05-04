package com.goldbank.shared.domain.usecase.account

import com.goldbank.shared.domain.model.Balance
import com.goldbank.shared.domain.repository.AccountRepository
import com.goldbank.shared.domain.util.Result

class GetBalanceUseCase(private val accountRepository: AccountRepository) {
    suspend operator fun invoke(accountId: String): Result<Balance> =
        accountRepository.getBalance(accountId)
}
