package com.unibank.app.nfc

/**
 * Processes ISO 7816 APDU commands for NFC HCE payments.
 * Handles SELECT, GPO, READ RECORD, and GENERATE AC commands.
 */
object ApduProcessor {

    // Status words
    val SW_OK = byteArrayOf(0x90.toByte(), 0x00.toByte())
    val SW_FILE_NOT_FOUND = byteArrayOf(0x6A.toByte(), 0x82.toByte())
    val SW_CONDITIONS_NOT_SATISFIED = byteArrayOf(0x69.toByte(), 0x85.toByte())
    val SW_WRONG_LENGTH = byteArrayOf(0x67.toByte(), 0x00.toByte())
    val SW_UNKNOWN = byteArrayOf(0x6F.toByte(), 0x00.toByte())

    // Visa AID
    private val VISA_AID = hexToBytes("A0000000041010")

    // Command types
    private const val CLA_ISO = 0x00
    private const val INS_SELECT = 0xA4
    private const val INS_GPO = 0xA8
    private const val INS_READ_RECORD = 0xB2
    private const val INS_GENERATE_AC = 0xAE

    data class ApduCommand(
        val cla: Int,
        val ins: Int,
        val p1: Int,
        val p2: Int,
        val data: ByteArray?,
        val le: Int?,
    )

    fun parse(apdu: ByteArray): ApduCommand? {
        if (apdu.size < 4) return null
        val cla = apdu[0].toInt() and 0xFF
        val ins = apdu[1].toInt() and 0xFF
        val p1 = apdu[2].toInt() and 0xFF
        val p2 = apdu[3].toInt() and 0xFF
        var data: ByteArray? = null
        var le: Int? = null

        if (apdu.size == 5) {
            le = apdu[4].toInt() and 0xFF
        } else if (apdu.size > 5) {
            val lc = apdu[4].toInt() and 0xFF
            if (apdu.size >= 5 + lc) {
                data = apdu.copyOfRange(5, 5 + lc)
                if (apdu.size > 5 + lc) {
                    le = apdu[5 + lc].toInt() and 0xFF
                }
            }
        }
        return ApduCommand(cla, ins, p1, p2, data, le)
    }

    fun isSelectCommand(cmd: ApduCommand): Boolean =
        cmd.ins == INS_SELECT && cmd.p1 == 0x04

    fun isVisaAid(data: ByteArray?): Boolean =
        data != null && data.contentEquals(VISA_AID)

    fun isGpoCommand(cmd: ApduCommand): Boolean =
        cmd.ins == INS_GPO

    fun isReadRecordCommand(cmd: ApduCommand): Boolean =
        cmd.ins == INS_READ_RECORD

    fun isGenerateAcCommand(cmd: ApduCommand): Boolean =
        cmd.ins == INS_GENERATE_AC

    /**
     * Build SELECT response with FCI (File Control Information).
     */
    fun buildSelectResponse(token: String): ByteArray {
        val fci = EmvTlvBuilder()
            .addTag("9F38", hexToBytes("9F02069F03069F1A029C019A039505")) // PDOL
            .addTag("84", VISA_AID) // DF Name
            .addTag("A5", EmvTlvBuilder() // FCI proprietary
                .addTag("50", "UniBank".toByteArray()) // Application label
                .build()
            )
            .build()
        val response = EmvTlvBuilder()
            .addTag("6F", fci)
            .build()
        return response + SW_OK
    }

    /**
     * Build GPO response with AIP and AFL.
     */
    fun buildGpoResponse(): ByteArray {
        val aip = byteArrayOf(0x00, 0x00) // Application Interchange Profile
        val afl = byteArrayOf(0x08, 0x01, 0x01, 0x00) // Application File Locator: SFI 1, records 1-1
        val data = EmvTlvBuilder()
            .addTag("80", aip + afl) // Response Message Template Format 1
            .build()
        return data + SW_OK
    }

    /**
     * Build READ RECORD response with card data.
     */
    fun buildReadRecordResponse(token: String, accountId: String): ByteArray {
        val record = EmvTlvBuilder()
            .addTag("70", EmvTlvBuilder() // EMV Record
                .addTag("5A", accountId.take(16).padEnd(16, '0').toByteArray()) // PAN
                .addTag("5F24", "301231".toByteArray()) // Expiry YYMMDD
                .addTag("9F26", token.take(8).toByteArray()) // Application Cryptogram (placeholder)
                .build()
            )
            .build()
        return record + SW_OK
    }

    /**
     * Build GENERATE AC response.
     */
    fun buildGenerateAcResponse(cryptogram: String): ByteArray {
        val data = EmvTlvBuilder()
            .addTag("80", EmvTlvBuilder()
                .addTag("9F27", byteArrayOf(0x80.toByte())) // CID: ARQC
                .addTag("9F26", cryptogram.take(16).toByteArray()) // Application Cryptogram
                .addTag("9F10", "0A01100000000000000000".toByteArray()) // Issuer Application Data
                .build()
            )
            .build()
        return data + SW_OK
    }

    fun hexToBytes(hex: String): ByteArray {
        val len = hex.length
        val data = ByteArray(len / 2)
        var i = 0
        while (i < len) {
            data[i / 2] = ((Character.digit(hex[i], 16) shl 4) + Character.digit(hex[i + 1], 16)).toByte()
            i += 2
        }
        return data
    }

    fun bytesToHex(bytes: ByteArray): String =
        bytes.joinToString("") { "%02X".format(it) }
}
