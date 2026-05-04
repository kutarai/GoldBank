package com.goldbank.shared.domain.usecase.account

import com.goldbank.shared.domain.model.Transaction
import com.goldbank.shared.domain.repository.AccountRepository
import kotlinx.coroutines.flow.Flow

class GetTransactionsUseCase(private val accountRepository: AccountRepository) {
    operator fun invoke(accountId: String): Flow<Transaction> =
        accountRepository.getTransactions(accountId)
}
