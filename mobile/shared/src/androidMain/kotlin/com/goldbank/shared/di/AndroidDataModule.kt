package com.goldbank.shared.di

import com.goldbank.shared.data.local.PreferencesManager
import com.goldbank.shared.data.local.SecureStorage
import com.goldbank.shared.data.local.SecurityPreferences
import com.goldbank.shared.data.local.SessionManager
import android.content.Context
import android.provider.Settings
import com.goldbank.shared.data.remote.GrpcChannelFactory
import com.goldbank.shared.data.remote.TokenRefresher
import com.goldbank.shared.data.remote.grpc.AccountGrpcClient
import com.goldbank.shared.data.remote.grpc.AiGrpcClient
import com.goldbank.shared.data.remote.grpc.AgentGrpcClient
import com.goldbank.shared.data.remote.grpc.AssetGrpcClient
import com.goldbank.shared.data.remote.grpc.BillPayGrpcClient
import com.goldbank.shared.data.remote.grpc.EkubGrpcClient
import com.goldbank.shared.data.remote.grpc.PaymentGrpcClient
import com.goldbank.shared.data.remote.grpc.KycGrpcClient
import com.goldbank.shared.data.remote.grpc.LoanGrpcClient
import com.goldbank.shared.data.remote.grpc.MerchantGrpcClient
import com.goldbank.shared.data.remote.grpc.TransferGrpcClient
import com.goldbank.shared.data.remote.interceptor.AuthClientInterceptor
import com.goldbank.shared.data.remote.interceptor.RetryInterceptor
import com.goldbank.shared.data.repository.AccountRepositoryImpl
import com.goldbank.shared.data.repository.AuthRepositoryImpl
import com.goldbank.shared.domain.repository.AccountRepository
import com.goldbank.shared.domain.repository.AuthRepository
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
    single { EkubGrpcClient(get()) }

    // Repositories
    single<AuthRepository> { AuthRepositoryImpl(get(), get()) }
    single<AccountRepository> { AccountRepositoryImpl(get()) }

    // Token refresher — checked before every gRPC call (see grpcCall).
    single {
        val context: Context = get()
        TokenRefresher(
            sessionManager = get(),
            authRepository = get(),
            deviceIdProvider = {
                Settings.Secure.getString(context.contentResolver, Settings.Secure.ANDROID_ID) ?: ""
            },
        )
    }
}
