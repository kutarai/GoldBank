package com.goldbank.shared.data.local

import android.content.Context
import android.content.SharedPreferences
import androidx.security.crypto.EncryptedSharedPreferences
import androidx.security.crypto.MasterKey

class SecureStorage(context: Context) {

    private val masterKey = MasterKey.Builder(context)
        .setKeyScheme(MasterKey.KeyScheme.AES256_GCM)
        .build()

    private val prefs: SharedPreferences = EncryptedSharedPreferences.create(
        context,
        "goldbank_secure_prefs",
        masterKey,
        EncryptedSharedPreferences.PrefKeyEncryptionScheme.AES256_SIV,
        EncryptedSharedPreferences.PrefValueEncryptionScheme.AES256_GCM,
    )

    var accessToken: String?
        get() = prefs.getString(KEY_ACCESS_TOKEN, null)
        set(value) = prefs.edit().putString(KEY_ACCESS_TOKEN, value).apply()

    var refreshToken: String?
        get() = prefs.getString(KEY_REFRESH_TOKEN, null)
        set(value) = prefs.edit().putString(KEY_REFRESH_TOKEN, value).apply()

    var accountId: String?
        get() = prefs.getString(KEY_ACCOUNT_ID, null)
        set(value) = prefs.edit().putString(KEY_ACCOUNT_ID, value).apply()

    var customerId: String?
        get() = prefs.getString(KEY_CUSTOMER_ID, null)
        set(value) = prefs.edit().putString(KEY_CUSTOMER_ID, value).apply()

    var accessTokenExpiresAt: Long
        get() = prefs.getLong(KEY_ACCESS_TOKEN_EXPIRES_AT, 0)
        set(value) = prefs.edit().putLong(KEY_ACCESS_TOKEN_EXPIRES_AT, value).apply()

    var phoneNumber: String?
        get() = prefs.getString(KEY_PHONE_NUMBER, null)
        set(value) = prefs.edit().putString(KEY_PHONE_NUMBER, value).apply()

    var nfcToken: String?
        get() = prefs.getString(KEY_NFC_TOKEN, null)
        set(value) = prefs.edit().putString(KEY_NFC_TOKEN, value).apply()

    fun clear() {
        prefs.edit().clear().apply()
    }

    companion object {
        private const val KEY_ACCESS_TOKEN = "access_token"
        private const val KEY_REFRESH_TOKEN = "refresh_token"
        private const val KEY_ACCOUNT_ID = "account_id"
        private const val KEY_CUSTOMER_ID = "customer_id"
        private const val KEY_ACCESS_TOKEN_EXPIRES_AT = "access_token_expires_at"
        private const val KEY_PHONE_NUMBER = "phone_number"
        private const val KEY_NFC_TOKEN = "nfc_token"
    }
}
