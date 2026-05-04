package com.goldbank.shared.di

import org.koin.core.module.Module
import org.koin.dsl.module

val sharedModule: Module = module {
    includes(domainModule)
}
