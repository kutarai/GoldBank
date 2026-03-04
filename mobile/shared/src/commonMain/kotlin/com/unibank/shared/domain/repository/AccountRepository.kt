package com.unibank.shared.domain.repository

import com.unibank.shared.domain.model.Balance
import com.unibank.shared.domain.model.Profile
import com.unibank.shared.domain.model.Transaction
import com.unibank.shared.domain.util.Result
import kotlinx.coroutines.flow.Flow

interface AccountRepository {
    suspend fun getProfile(accountId: String): Result<Profile>
    suspend fun updateProfile(
        accountId: String,
        firstName: String? = null,
        lastName: String? = null,
        email: String? = null,
        dateOfBirth: String? = null,
        nationalId: String? = null,
    ): Result<Profile>
    suspend fun getBalance(accountId: String): Result<Balance>
    fun getTransactions(
        accountId: String,
        fromDate: String? = null,
        toDate: String? = null,
        typeFilter: String? = null,
        statusFilter: String? = null,
    ): Flow<Transaction>
}
