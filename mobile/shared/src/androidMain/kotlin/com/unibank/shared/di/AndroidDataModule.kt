package com.unibank.shared.di

import com.unibank.shared.data.local.PreferencesManager
import com.unibank.shared.data.local.SecureStorage
import com.unibank.shared.data.local.SecurityPreferences
import com.unibank.shared.data.local.SessionManager
import com.unibank.shared.data.remote.GrpcChannelFactory
import com.unibank.shared.data.remote.grpc.AccountGrpcClient
import com.unibank.shared.data.remote.grpc.AiGrpcClient
import com.unibank.shared.data.remote.grpc.AgentGrpcClient
import com.unibank.shared.data.remote.grpc.AssetGrpcClient
import com.unibank.shared.data.remote.grpc.BillPayGrpcClient
import com.unibank.shared.data.remote.grpc.PaymentGrpcClient
import com.unibank.shared.data.remote.grpc.KycGrpcClient
import com.unibank.shared.data.remote.grpc.LoanGrpcClient
import com.unibank.shared.data.remote.grpc.MerchantGrpcClient
import com.unibank.shared.data.remote.grpc.TransferGrpcClient
import com.unibank.shared.data.remote.interceptor.AuthClientInterceptor
import com.unibank.shared.data.remote.interceptor.RetryInterceptor
import com.unibank.shared.data.repository.AccountRepositoryImpl
import com.unibank.shared.data.repository.AuthRepositoryImpl
import com.unibank.shared.domain.repository.AccountRepository
import com.unibank.shared.domain.repository.AuthRepository
import io.grpc.ManagedChannel
import org.koin.dsl.module

fun androidDataModule(
    grpcHost: String,
    grpcPort: Int,
    useTls: Boolean,
    defaultTenantId: String,
) = module {
    // Local storage
    single { SecureStorage(get()) }
    single { SecurityPreferences(get()) }
    single { SessionManager(get(), defaultTenantId) }
    single { PreferencesManager(get()) }

    // gRPC interceptors
    single {
        AuthClientInterceptor(
            tokenProvider = { get<SessionManager>().getAccessToken() },
            tenantIdProvider = { get<SessionManager>().currentTenantId },
        )
    }
    single { RetryInterceptor() }

    // gRPC channel
    single {
        GrpcChannelFactory(
            host = grpcHost,
            port = grpcPort,
            useTls = useTls,
            interceptors = listOf(get<AuthClientInterceptor>(), get<RetryInterceptor>()),
        )
    }
    single<ManagedChannel> { get<GrpcChannelFactory>().create() }

    // gRPC clients
    single { AccountGrpcClient(get()) }
    single { PaymentGrpcClient(get()) }
    single { TransferGrpcClient(get()) }
    single { BillPayGrpcClient(get()) }
    single { AgentGrpcClient(get()) }
    single { KycGrpcClient(get()) }
    single { LoanGrpcClient(get()) }
    single { MerchantGrpcClient(get()) }
    single { AiGrpcClient(get()) }
    single { AssetGrpcClient(get()) }

    // Repositories
    single<AuthRepository> { AuthRepositoryImpl(get(), get()) }
    single<AccountRepository> { AccountRepositoryImpl(get()) }
}
