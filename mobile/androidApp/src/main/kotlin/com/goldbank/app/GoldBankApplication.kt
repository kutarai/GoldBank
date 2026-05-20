package com.goldbank.app

import android.app.Application
import com.goldbank.app.di.presentationModule
import com.goldbank.shared.di.androidDataModule
import com.goldbank.shared.di.sharedModule
import org.koin.android.ext.koin.androidContext
import org.koin.android.ext.koin.androidLogger
import org.koin.core.context.startKoin
import timber.log.Timber

class GoldBankApplication : Application() {

    override fun onCreate() {
        super.onCreate()

        if (BuildConfig.DEBUG) {
            Timber.plant(Timber.DebugTree())
        }

        startKoin {
            androidLogger()
            androidContext(this@GoldBankApplication)
            modules(
                sharedModule,
                androidDataModule(
                    grpcHost = BuildConfig.GRPC_HOST,
                    grpcPort = BuildConfig.GRPC_PORT,
                    useTls = BuildConfig.GRPC_USE_TLS,
                    defaultTenantId = BuildConfig.DEFAULT_TENANT_ID,
                ),
                presentationModule,
            )
        }
    }
}
