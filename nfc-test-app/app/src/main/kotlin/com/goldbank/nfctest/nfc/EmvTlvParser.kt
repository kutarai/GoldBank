package com.goldbank.nfctest.nfc

/**
 * Parses and builds EMV TLV (Tag-Length-Value) data structures.
 * Copied from com.goldbank.app.nfc.EmvTlvParser
 */
object EmvTlvParser {

    data class TlvEntry(val tag: String, val value: ByteArray) {
        override fun equals(other: Any?): Boolean {
            if (this === other) return true
            if (other !is TlvEntry) return false
            return tag == other.tag && value.contentEquals(other.value)
        }
        override fun hashCode(): Int = 31 * tag.hashCode() + value.contentHashCode()
    }

    fun parse(data: ByteArray): List<TlvEntry> {
        val entries = mutableListOf<TlvEntry>()
        var offset = 0

        while (offset < data.size) {
            if (offset >= data.size) break

            val tagStart = offset
            val tagByte = data[offset].toInt() and 0xFF
            offset++

            if (tagByte and 0x1F == 0x1F) {
                while (offset < data.size) {
                    val next = data[offset].toInt() and 0xFF
                    offset++
                    if (next and 0x80 == 0) break
                }
            }
            val tag = ApduProcessor.bytesToHex(data.copyOfRange(tagStart, offset))

            if (offset >= data.size) break

            var length = data[offset].toInt() and 0xFF
            offset++
            if (length == 0x81 && offset < data.size) {
                length = data[offset].toInt() and 0xFF
                offset++
            } else if (length == 0x82 && offset + 1 < data.size) {
                length = ((data[offset].toInt() and 0xFF) shl 8) or (data[offset + 1].toInt() and 0xFF)
                offset += 2
            }

            if (offset + length > data.size) break

            val value = data.copyOfRange(offset, offset + length)
            offset += length

            entries.add(TlvEntry(tag, value))
        }

        return entries
    }

    fun findTag(entries: List<TlvEntry>, tag: String): ByteArray? =
        entries.find { it.tag.equals(tag, ignoreCase = true) }?.value

    fun extractAmount(pdolData: ByteArray): Long {
        if (pdolData.size < 6) return 0
        val amountBytes = pdolData.copyOfRange(0, 6)
        return ApduProcessor.bytesToHex(amountBytes).toLongOrNull() ?: 0
    }
}

class EmvTlvBuilder {
    private val buffer = mutableListOf<Byte>()

    fun addTag(tagHex: String, value: ByteArray): EmvTlvBuilder {
        val tagBytes = ApduProcessor.hexToBytes(tagHex)
        buffer.addAll(tagBytes.toList())

        val length = value.size
        if (length < 0x80) {
            buffer.add(length.toByte())
        } else if (length < 0x100) {
            buffer.add(0x81.toByte())
            buffer.add(length.toByte())
        } else {
            buffer.add(0x82.toByte())
            buffer.add((length shr 8).toByte())
            buffer.add((length and 0xFF).toByte())
        }

        buffer.addAll(value.toList())

        return this
    }

    fun build(): ByteArray = buffer.toByteArray()
}
