package com.goldbank.nfctest.nfc

import android.content.Context
import android.content.SharedPreferences
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

/**
 * File-backed NFC event log shared between HceService and MainActivity.
 * Uses SharedPreferences to work reliably across Android components.
 */
object NfcLog {
    private const val PREFS_NAME = "nfc_event_log"
    private const val KEY_EVENTS = "events"
    private const val KEY_COUNT = "event_count"
    private const val MAX_EVENTS = 50

    private var prefs: SharedPreferences? = null

    fun init(context: Context) {
        if (prefs == null) {
            prefs = context.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE)
        }
    }

    fun addEvent(status: String, command: String = "", response: String = "", state: String = "") {
        val p = prefs ?: return
        val time = SimpleDateFormat("HH:mm:ss.SSS", Locale.getDefault()).format(Date())
        val entry = "$time|$status|$command|$response|$state"

        val existing = p.getString(KEY_EVENTS, "") ?: ""
        val lines = existing.split("\n").filter { it.isNotBlank() }.toMutableList()
        lines.add(0, entry)
        while (lines.size > MAX_EVENTS) lines.removeAt(lines.size - 1)

        val count = p.getInt(KEY_COUNT, 0)
        p.edit()
            .putString(KEY_EVENTS, lines.joinToString("\n"))
            .putInt(KEY_COUNT, count + 1)
            .apply()
    }

    fun getEvents(): List<ParsedEvent> {
        val p = prefs ?: return emptyList()
        val raw = p.getString(KEY_EVENTS, "") ?: ""
        return raw.split("\n").filter { it.isNotBlank() }.map { line ->
            val parts = line.split("|", limit = 5)
            ParsedEvent(
                timestamp = parts.getOrElse(0) { "" },
                status = parts.getOrElse(1) { "" },
                command = parts.getOrElse(2) { "" },
                response = parts.getOrElse(3) { "" },
                state = parts.getOrElse(4) { "" },
            )
        }
    }

    fun getCount(): Int = prefs?.getInt(KEY_COUNT, 0) ?: 0

    fun clear() {
        prefs?.edit()?.clear()?.apply()
    }

    data class ParsedEvent(
        val timestamp: String,
        val status: String,
        val command: String,
        val response: String,
        val state: String,
    )
}
