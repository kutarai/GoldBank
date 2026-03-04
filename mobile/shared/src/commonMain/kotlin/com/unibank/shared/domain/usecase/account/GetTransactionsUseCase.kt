package com.unibank.shared.domain.usecase.account

import com.unibank.shared.domain.model.Transaction
import com.unibank.shared.domain.repository.AccountRepository
import kotlinx.coroutines.flow.Flow

class GetTransactionsUseCase(private val accountRepository: AccountRepository) {
    operator fun invoke(accountId: String): Flow<Transaction> =
        accountRepository.getTransactions(accountId)
}
