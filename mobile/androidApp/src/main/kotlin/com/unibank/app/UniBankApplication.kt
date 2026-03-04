package com.unibank.app

import android.app.Application
import com.unibank.app.di.presentationModule
import com.unibank.shared.di.androidDataModule
import com.unibank.shared.di.sharedModule
import org.koin.android.ext.koin.androidContext
import org.koin.android.ext.koin.androidLogger
import org.koin.core.context.startKoin
import timber.log.Timber

class UniBankApplication : Application() {

    override fun onCreate() {
        super.onCreate()

        if (BuildConfig.DEBUG) {
            Timber.plant(Timber.DebugTree())
        }

        startKoin {
            androidLogger()
            androidContext(this@UniBankApplication)
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
