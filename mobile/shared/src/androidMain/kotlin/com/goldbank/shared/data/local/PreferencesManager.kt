package com.goldbank.shared.data.local

import android.content.Context
import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.booleanPreferencesKey
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.stringPreferencesKey
import androidx.datastore.preferences.preferencesDataStore
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map

private val Context.dataStore: DataStore<Preferences> by preferencesDataStore(name = "goldbank_prefs")

class PreferencesManager(private val context: Context) {

    val language: Flow<String> = context.dataStore.data.map { prefs ->
        prefs[KEY_LANGUAGE] ?: "en"
    }

    val isDarkMode: Flow<Boolean> = context.dataStore.data.map { prefs ->
        prefs[KEY_DARK_MODE] ?: false
    }

    val notificationsEnabled: Flow<Boolean> = context.dataStore.data.map { prefs ->
        prefs[KEY_NOTIFICATIONS] ?: true
    }

    suspend fun setLanguage(language: String) {
        context.dataStore.edit { it[KEY_LANGUAGE] = language }
    }

    suspend fun setDarkMode(enabled: Boolean) {
        context.dataStore.edit { it[KEY_DARK_MODE] = enabled }
    }

    suspend fun setNotificationsEnabled(enabled: Boolean) {
        context.dataStore.edit { it[KEY_NOTIFICATIONS] = enabled }
    }

    companion object {
        private val KEY_LANGUAGE = stringPreferencesKey("language")
        private val KEY_DARK_MODE = booleanPreferencesKey("dark_mode")
        private val KEY_NOTIFICATIONS = booleanPreferencesKey("notifications_enabled")
    }
}
