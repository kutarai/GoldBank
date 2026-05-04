package com.goldbank.nfctest.nfc

import android.nfc.cardemulation.HostApduService
import android.os.Bundle
import android.util.Log
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale
import java.util.concurrent.CopyOnWriteArrayList

/**
 * Host Card Emulation service for NFC contactless payments.
 * Standalone version — no backend dependency.
 */
class HceService : HostApduService() {

    private var currentToken: String? = null
    private var currentPan: String? = null
    private var transactionState = TransactionState.IDLE

    enum class TransactionState { IDLE, SELECTED, GPO_DONE, RECORD_READ, COMPLETED }

    data class ApduEvent(
        val timestamp: String,
        val status: String,
        val command: String,
        val response: String,
        val state: String,
    )

    companion object {
        const val TAG = "HceService"

        var configuredPan: String = "6275000000001234"
        var configuredToken: String = "TESTTOKEN001"

        // Shared event log — accessed by both HceService and MainActivity
        val eventLog = CopyOnWriteArrayList<ApduEvent>()
        var lastState: String = "IDLE"
        var lastStatus: String = ""

        fun addEvent(status: String, command: String = "", response: String = "", state: String = "IDLE") {
            val time = SimpleDateFormat("HH:mm:ss.SSS", Locale.getDefault()).format(Date())
            eventLog.add(0, ApduEvent(time, status, command, response, state))
            lastState = state
            lastStatus = status
            while (eventLog.size > 50) eventLog.removeAt(eventLog.size - 1)
        }
    }

    override fun onCreate() {
        super.onCreate()
        NfcLog.init(applicationContext)
        currentPan = configuredPan
        currentToken = configuredToken
        Log.d(TAG, "HCE Service created, PAN: ${maskPan(currentPan)}, token: ${currentToken != null}")
        NfcLog.addEvent("SERVICE_CREATED", command = "PAN: ${maskPan(currentPan)}", state = "IDLE")
    }

    override fun processCommandApdu(commandApdu: ByteArray, extras: Bundle?): ByteArray {
        val hexCmd = ApduProcessor.bytesToHex(commandApdu)
        Log.d(TAG, "APDU received: $hexCmd")

        val cmd = ApduProcessor.parse(commandApdu) ?: run {
            NfcLog.addEvent("error", command = hexCmd, response = "PARSE_FAIL")
            return ApduProcessor.SW_UNKNOWN
        }

        val (response, label) = when {
            ApduProcessor.isSelectCommand(cmd) -> handleSelect(cmd) to "SELECT"
            ApduProcessor.isGpoCommand(cmd) -> handleGpo(cmd) to "GPO"
            ApduProcessor.isReadRecordCommand(cmd) -> handleReadRecord(cmd) to "READ_RECORD"
            ApduProcessor.isGenerateAcCommand(cmd) -> handleGenerateAc(cmd) to "GENERATE_AC"
            else -> ApduProcessor.SW_UNKNOWN to "UNKNOWN(INS=${"%02X".format(cmd.ins)})"
        }

        NfcLog.addEvent(
            status = label,
            command = hexCmd,
            response = ApduProcessor.bytesToHex(response),
            state = transactionState.name
        )

        return response
    }

    private fun handleSelect(cmd: ApduProcessor.ApduCommand): ByteArray {
        if (!ApduProcessor.isKnownAid(cmd.data)) {
            val aidHex = cmd.data?.let { ApduProcessor.bytesToHex(it) } ?: "null"
            Log.w(TAG, "Unknown AID: $aidHex")
            addEvent("UNKNOWN_AID", command = aidHex, response = "6A82", state = "IDLE")
            return ApduProcessor.SW_FILE_NOT_FOUND
        }
        val token = currentToken ?: return ApduProcessor.SW_CONDITIONS_NOT_SATISFIED
        cmd.data?.let { ApduProcessor.selectedAid = it }
        transactionState = TransactionState.SELECTED
        Log.d(TAG, "AID selected: ${ApduProcessor.bytesToHex(ApduProcessor.selectedAid)}")
        return ApduProcessor.buildSelectResponse(token)
    }

    private fun handleGpo(cmd: ApduProcessor.ApduCommand): ByteArray {
        if (transactionState != TransactionState.SELECTED) {
            return ApduProcessor.SW_CONDITIONS_NOT_SATISFIED
        }
        transactionState = TransactionState.GPO_DONE
        Log.d(TAG, "GPO processed")
        return ApduProcessor.buildGpoResponse()
    }

    private fun handleReadRecord(cmd: ApduProcessor.ApduCommand): ByteArray {
        if (transactionState != TransactionState.GPO_DONE) {
            return ApduProcessor.SW_CONDITIONS_NOT_SATISFIED
        }
        val token = currentToken ?: return ApduProcessor.SW_CONDITIONS_NOT_SATISFIED
        val pan = currentPan ?: return ApduProcessor.SW_CONDITIONS_NOT_SATISFIED
        transactionState = TransactionState.RECORD_READ
        Log.d(TAG, "Read record returned, PAN: ${maskPan(pan)}")
        return ApduProcessor.buildReadRecordResponse(token, pan)
    }

    private fun handleGenerateAc(cmd: ApduProcessor.ApduCommand): ByteArray {
        if (transactionState != TransactionState.RECORD_READ) {
            return ApduProcessor.SW_CONDITIONS_NOT_SATISFIED
        }
        val token = currentToken ?: return ApduProcessor.SW_CONDITIONS_NOT_SATISFIED
        transactionState = TransactionState.COMPLETED

        val nfcData = cmd.data?.let { ApduProcessor.bytesToHex(it) } ?: ""
        Log.d(TAG, "Generate AC — transaction complete, data: $nfcData")

        return ApduProcessor.buildGenerateAcResponse(token)
    }

    override fun onDeactivated(reason: Int) {
        Log.d(TAG, "HCE deactivated: reason=$reason")
        transactionState = TransactionState.IDLE
        addEvent("DEACTIVATED", state = "IDLE")
    }

    private fun maskPan(pan: String?): String {
        if (pan == null || pan.length < 8) return pan ?: "null"
        return "**** **** **** " + pan.takeLast(4)
    }
}
