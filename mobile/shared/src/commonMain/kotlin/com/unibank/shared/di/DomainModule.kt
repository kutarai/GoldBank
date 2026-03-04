package com.unibank.shared.di

import com.unibank.shared.domain.usecase.account.GetBalanceUseCase
import com.unibank.shared.domain.usecase.account.GetProfileUseCase
import com.unibank.shared.domain.usecase.account.GetTransactionsUseCase
import com.unibank.shared.domain.usecase.auth.CreatePinUseCase
import com.unibank.shared.domain.usecase.auth.LoginUseCase
import com.unibank.shared.domain.usecase.auth.LogoutUseCase
import com.unibank.shared.domain.usecase.auth.RegisterUseCase
import com.unibank.shared.domain.usecase.auth.VerifyOtpUseCase
import org.koin.dsl.module

val domainModule = module {
    // Auth use cases
    factory { RegisterUseCase(get()) }
    factory { VerifyOtpUseCase(get()) }
    factory { CreatePinUseCase(get()) }
    factory { LoginUseCase(get()) }
    factory { LogoutUseCase(get()) }

    // Account use cases
    factory { GetBalanceUseCase(get()) }
    factory { GetProfileUseCase(get()) }
    factory { GetTransactionsUseCase(get()) }
}
