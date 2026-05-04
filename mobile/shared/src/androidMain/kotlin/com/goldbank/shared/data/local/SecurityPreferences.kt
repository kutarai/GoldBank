package com.goldbank.shared.data.local

import android.content.Context
import android.content.SharedPreferences
import androidx.security.crypto.EncryptedSharedPreferences
import androidx.security.crypto.MasterKey

class SecurityPreferences(context: Context) {
    private val masterKey = MasterKey.Builder(context)
        .setKeyScheme(MasterKey.KeyScheme.AES256_GCM)
        .build()

    private val prefs: SharedPreferences = EncryptedSharedPreferences.create(
        context,
        "goldbank_security_prefs",
        masterKey,
        EncryptedSharedPreferences.PrefKeyEncryptionScheme.AES256_SIV,
        EncryptedSharedPreferences.PrefValueEncryptionScheme.AES256_GCM,
    )

    var biometricEnabled: Boolean
        get() = prefs.getBoolean("biometric_enabled", false)
        set(value) = prefs.edit().putBoolean("biometric_enabled", value).apply()

    var inactivityTimeoutMinutes: Int
        get() = prefs.getInt("inactivity_timeout_minutes", 3)
        set(value) = prefs.edit().putInt("inactivity_timeout_minutes", value).apply()

    var lastActiveTimestamp: Long
        get() = prefs.getLong("last_active_timestamp", 0L)
        set(value) = prefs.edit().putLong("last_active_timestamp", value).apply()
}
