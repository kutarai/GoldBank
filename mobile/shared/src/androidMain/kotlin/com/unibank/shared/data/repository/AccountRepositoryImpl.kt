package com.unibank.shared.data.repository

import com.unibank.shared.data.remote.grpc.AccountGrpcClient
import com.unibank.shared.domain.model.Balance
import com.unibank.shared.domain.model.Profile
import com.unibank.shared.domain.model.Transaction
import com.unibank.shared.domain.repository.AccountRepository
import com.unibank.shared.domain.util.Result
import kotlinx.coroutines.flow.Flow

class AccountRepositoryImpl(
    private val accountClient: AccountGrpcClient,
) : AccountRepository {

    override suspend fun getProfile(accountId: String): Result<Profile> =
        accountClient.getProfile(accountId)

    override suspend fun updateProfile(
        accountId: String,
        firstName: String?,
        lastName: String?,
        email: String?,
        dateOfBirth: String?,
        nationalId: String?,
    ): Result<Profile> {
        // UpdateProfile RPC not yet in AccountGrpcClient — will be added in Phase 11
        return Result.Failure(
            com.unibank.shared.domain.util.AppError.Server("NOT_IMPLEMENTED", "Not yet implemented")
        )
    }

    override suspend fun getBalance(accountId: String): Result<Balance> =
        accountClient.getBalance(accountId)

    override fun getTransactions(
        accountId: String,
        fromDate: String?,
        toDate: String?,
        typeFilter: String?,
        statusFilter: String?,
    ): Flow<Transaction> =
        accountClient.getTransactions(accountId)
}
