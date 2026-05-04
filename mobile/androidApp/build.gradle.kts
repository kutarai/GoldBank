plugins {
    alias(libs.plugins.android.application)
    alias(libs.plugins.kotlin.android)
    alias(libs.plugins.kotlin.compose.compiler)
    alias(libs.plugins.kotlin.serialization)
}

android {
    namespace = "com.goldbank.app"
    compileSdk = 36

    defaultConfig {
        applicationId = "com.goldbank.app"
        minSdk = 26
        targetSdk = 35
        versionCode = 1
        versionName = "1.0.0"

        testInstrumentationRunner = "androidx.test.runner.AndroidJUnitRunner"
    }

    buildTypes {
        debug {
            buildConfigField("String", "GRPC_HOST", "\"10.0.2.2\"")
            buildConfigField("int", "GRPC_PORT", "5000")
            buildConfigField("boolean", "GRPC_USE_TLS", "false")
            // Match the server-side demo seed and the bank-teller JWT tenant.
            buildConfigField("String", "DEFAULT_TENANT_ID", "\"goldbank\"")
        }
        release {
            isMinifyEnabled = true
            isShrinkResources = true
            proguardFiles(getDefaultProguardFile("proguard-android-optimize.txt"), "proguard-rules.pro")
            buildConfigField("String", "GRPC_HOST", "\"api.goldbank.co.zw\"")
            buildConfigField("int", "GRPC_PORT", "443")
            buildConfigField("boolean", "GRPC_USE_TLS", "true")
            buildConfigField("String", "DEFAULT_TENANT_ID", "\"goldbank\"")
        }
    }

    buildFeatures {
        compose = true
        buildConfig = true
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }
    kotlinOptions {
        jvmTarget = "17"
    }
}

dependencies {
    implementation(project(":shared"))

    // Compose BOM
    implementation(platform(libs.compose.bom))
    implementation(libs.bundles.compose)
    debugImplementation(libs.compose.ui.tooling)

    // AndroidX
    implementation(libs.core.ktx)
    implementation(libs.bundles.lifecycle)
    implementation(libs.activity.compose)
    implementation(libs.navigation.compose)
    implementation(libs.biometric)
    implementation(libs.camerak)
    implementation(libs.accompanist.permissions)

    // Koin
    implementation(platform(libs.koin.bom))
    implementation(libs.bundles.koin)

    // Image loading
    implementation(libs.coil.compose)
    implementation(libs.coil.network)

    // QR Code
    implementation(libs.zxing.core)
    implementation(libs.zxing.android)

    // Serialization (needed for type-safe Navigation Compose routes)
    implementation(libs.kotlinx.serialization.json)

    // Logging
    implementation(libs.timber)

    // Testing
    testImplementation(libs.junit)
    testImplementation(libs.mockk)
    testImplementation(libs.kotlinx.coroutines.test)
    testImplementation(libs.turbine)
}
