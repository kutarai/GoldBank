package com.unibank.app.nfc

import android.content.Intent
import android.nfc.cardemulation.HostApduService
import android.os.Bundle
import com.unibank.shared.data.local.SessionManager
import com.unibank.shared.data.remote.grpc.PaymentGrpcClient
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.launch
import org.koin.android.ext.android.inject
import timber.log.Timber

/**
 * Host Card Emulation service for NFC contactless payments.
 * Emulates a Visa payment card using tokenized credentials.
 */
class HceService : HostApduService() {

    private val sessionManager: SessionManager by inject()
    private val paymentClient: PaymentGrpcClient by inject()
    private val serviceScope = CoroutineScope(SupervisorJob() + Dispatchers.IO)

    private var currentToken: String? = null
    private var currentAccountId: String? = null
    private var transactionState = TransactionState.IDLE

    enum class TransactionState { IDLE, SELECTED, GPO_DONE, RECORD_READ, COMPLETED }

    companion object {
        const val ACTION_NFC_PAYMENT_STATUS = "com.unibank.app.NFC_PAYMENT_STATUS"
        const val EXTRA_STATUS = "status"
        const val EXTRA_TRANSACTION_ID = "transaction_id"
        const val EXTRA_REQUIRES_PIN = "requires_pin"
        const val EXTRA_AMOUNT = "amount"
    }

    override fun onCreate() {
        super.onCreate()
        currentAccountId = sessionManager.getAccountId()
        currentToken = sessionManager.getNfcToken()
        Timber.d("HCE Service created, token available: ${currentToken != null}")
    }

    override fun processCommandApdu(commandApdu: ByteArray, extras: Bundle?): ByteArray {
        Timber.d("APDU received: ${ApduProcessor.bytesToHex(commandApdu)}")

        val cmd = ApduProcessor.parse(commandApdu) ?: return ApduProcessor.SW_UNKNOWN

        return when {
            ApduProcessor.isSelectCommand(cmd) -> handleSelect(cmd)
            ApduProcessor.isGpoCommand(cmd) -> handleGpo(cmd)
            ApduProcessor.isReadRecordCommand(cmd) -> handleReadRecord(cmd)
            ApduProcessor.isGenerateAcCommand(cmd) -> handleGenerateAc(cmd)
            else -> ApduProcessor.SW_UNKNOWN
        }
    }

    private fun handleSelect(cmd: ApduProcessor.ApduCommand): ByteArray {
        if (!ApduProcessor.isVisaAid(cmd.data)) {
            Timber.w("Unknown AID selected")
            return ApduProcessor.SW_FILE_NOT_FOUND
        }
        val token = currentToken ?: return ApduProcessor.SW_CONDITIONS_NOT_SATISFIED
        transactionState = TransactionState.SELECTED
        Timber.d("AID selected, returning FCI")
        return ApduProcessor.buildSelectResponse(token)
    }

    private fun handleGpo(cmd: ApduProcessor.ApduCommand): ByteArray {
        if (transactionState != TransactionState.SELECTED) {
            return ApduProcessor.SW_CONDITIONS_NOT_SATISFIED
        }
        transactionState = TransactionState.GPO_DONE
        Timber.d("GPO processed")
        return ApduProcessor.buildGpoResponse()
    }

    private fun handleReadRecord(cmd: ApduProcessor.ApduCommand): ByteArray {
        if (transactionState != TransactionState.GPO_DONE) {
            return ApduProcessor.SW_CONDITIONS_NOT_SATISFIED
        }
        val token = currentToken ?: return ApduProcessor.SW_CONDITIONS_NOT_SATISFIED
        val accountId = currentAccountId ?: return ApduProcessor.SW_CONDITIONS_NOT_SATISFIED
        transactionState = TransactionState.RECORD_READ
        Timber.d("Read record returned")
        return ApduProcessor.buildReadRecordResponse(token, accountId)
    }

    private fun handleGenerateAc(cmd: ApduProcessor.ApduCommand): ByteArray {
        if (transactionState != TransactionState.RECORD_READ) {
            return ApduProcessor.SW_CONDITIONS_NOT_SATISFIED
        }
        val token = currentToken ?: return ApduProcessor.SW_CONDITIONS_NOT_SATISFIED
        transactionState = TransactionState.COMPLETED

        val nfcData = cmd.data?.let { ApduProcessor.bytesToHex(it) } ?: ""

        serviceScope.launch { submitPayment(nfcData) }

        Timber.d("Generate AC — payment submitted")
        return ApduProcessor.buildGenerateAcResponse(token)
    }

    private suspend fun submitPayment(nfcData: String) {
        val accountId = currentAccountId ?: return
        val intent = Intent(ACTION_NFC_PAYMENT_STATUS).apply {
            putExtra(EXTRA_STATUS, "processing")
            putExtra(EXTRA_AMOUNT, nfcData)
        }
        sendBroadcast(intent)
    }

    override fun onDeactivated(reason: Int) {
        Timber.d("HCE deactivated: reason=$reason")
        transactionState = TransactionState.IDLE
    }

    override fun onDestroy() {
        super.onDestroy()
        serviceScope.cancel()
    }
}
