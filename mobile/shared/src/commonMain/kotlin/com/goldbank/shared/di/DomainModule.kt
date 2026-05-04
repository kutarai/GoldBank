package com.goldbank.shared.di

import com.goldbank.shared.domain.usecase.account.GetBalanceUseCase
import com.goldbank.shared.domain.usecase.account.GetProfileUseCase
import com.goldbank.shared.domain.usecase.account.GetTransactionsUseCase
import com.goldbank.shared.domain.usecase.auth.CreatePinUseCase
import com.goldbank.shared.domain.usecase.auth.LoginUseCase
import com.goldbank.shared.domain.usecase.auth.LogoutUseCase
import com.goldbank.shared.domain.usecase.auth.RegisterUseCase
import com.goldbank.shared.domain.usecase.auth.VerifyOtpUseCase
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
