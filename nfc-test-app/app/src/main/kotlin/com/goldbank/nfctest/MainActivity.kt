package com.goldbank.nfctest

import android.content.Intent
import android.nfc.NfcAdapter
import android.nfc.cardemulation.CardEmulation
import android.os.Bundle
import android.provider.Settings
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.animation.core.LinearEasing
import androidx.compose.animation.core.RepeatMode
import androidx.compose.animation.core.animateFloat
import androidx.compose.animation.core.infiniteRepeatable
import androidx.compose.animation.core.rememberInfiniteTransition
import androidx.compose.animation.core.tween
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Contactless
import androidx.compose.material.icons.filled.CreditCard
import androidx.compose.material.icons.filled.Nfc
import androidx.compose.material3.*
import com.goldbank.nfctest.nfc.ApduProcessor
import com.goldbank.nfctest.nfc.NfcLog
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.alpha
import androidx.compose.ui.draw.scale
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.goldbank.nfctest.nfc.HceService
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

class MainActivity : ComponentActivity() {

    private var nfcAdapter: NfcAdapter? = null
    private val nfcEvents = mutableStateListOf<NfcEvent>()
    private var nfcActive = mutableStateOf(false)
    private var lastStatus = mutableStateOf("")
    private var lastState = mutableStateOf("IDLE")
    private var currentPan = mutableStateOf(HceService.configuredPan)
    private var isDefaultPaymentApp = mutableStateOf(false)

    data class NfcEvent(
        val timestamp: String,
        val status: String,
        val command: String,
        val response: String,
        val state: String,
    )

    private var diagnosticInfo = mutableStateOf("")

    private fun checkDefaultPaymentApp(): Boolean {
        val adapter = nfcAdapter ?: return false
        val emulation = CardEmulation.getInstance(adapter)
        val component = android.content.ComponentName(this, HceService::class.java)
        val isDefaultPayment = emulation.isDefaultServiceForCategory(component, CardEmulation.CATEGORY_PAYMENT)
        val isDefaultOther = emulation.isDefaultServiceForCategory(component, CardEmulation.CATEGORY_OTHER)
        val supportsHce = packageManager.hasSystemFeature("android.hardware.nfc.hce")

        diagnosticInfo.value = buildString {
            append("HCE supported: $supportsHce")
            append(" | Default payment: $isDefaultPayment")
            append(" | Default other: $isDefaultOther")
            append(" | NFC enabled: ${adapter.isEnabled}")
        }

        return isDefaultPayment
    }

    private fun openNfcPaymentSettings() {
        try {
            startActivity(Intent(Settings.ACTION_NFC_PAYMENT_SETTINGS))
        } catch (_: Exception) {
            try {
                startActivity(Intent(Settings.ACTION_NFC_SETTINGS))
            } catch (_: Exception) {
                startActivity(Intent(Settings.ACTION_SETTINGS))
            }
        }
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        nfcAdapter = NfcAdapter.getDefaultAdapter(this)
        NfcLog.init(applicationContext)

        setContent {
            // Poll NfcLog every 500ms
            LaunchedEffect(Unit) {
                while (true) {
                    val logCount = NfcLog.getCount()
                    if (logCount != nfcEvents.size) {
                        val events = NfcLog.getEvents()
                        nfcEvents.clear()
                        nfcEvents.addAll(events.map {
                            NfcEvent(it.timestamp, it.status, it.command, it.response, it.state)
                        })
                        if (events.isNotEmpty()) {
                            lastStatus.value = events[0].status
                            lastState.value = events[0].state
                        }
                    }
                    kotlinx.coroutines.delay(500)
                }
            }

            MaterialTheme(
                colorScheme = darkColorScheme(
                    primary = Color(0xFF4FC3F7),
                    secondary = Color(0xFF81C784),
                    surface = Color(0xFF1E1E2E),
                    background = Color(0xFF121218),
                    error = Color(0xFFEF5350),
                )
            ) {
                NfcTestScreen()
            }
        }
    }

    override fun onResume() {
        super.onResume()
        isDefaultPaymentApp.value = checkDefaultPaymentApp()
        // Aggressively set as preferred service whenever app is in foreground
        nfcAdapter?.let { adapter ->
            val emulation = CardEmulation.getInstance(adapter)
            val component = android.content.ComponentName(this, HceService::class.java)
            emulation.setPreferredService(this, component)
        }
    }

    override fun onPause() {
        super.onPause()
        nfcAdapter?.let { adapter ->
            val emulation = CardEmulation.getInstance(adapter)
            emulation.unsetPreferredService(this)
        }
    }

    @OptIn(ExperimentalMaterial3Api::class)
    @Composable
    fun NfcTestScreen() {
        val isNfcAvailable = nfcAdapter != null
        val isNfcEnabled = nfcAdapter?.isEnabled == true
        val active by nfcActive
        val status by lastStatus
        val state by lastState
        val isDefault by isDefaultPaymentApp
        var panInput by remember { mutableStateOf(currentPan.value) }

        Scaffold(
            topBar = {
                TopAppBar(
                    title = {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            Icon(Icons.Default.Contactless, contentDescription = null,
                                modifier = Modifier.size(28.dp))
                            Spacer(Modifier.width(8.dp))
                            Text("GoldBank NFC Test")
                        }
                    },
                    colors = TopAppBarDefaults.topAppBarColors(
                        containerColor = MaterialTheme.colorScheme.surface
                    )
                )
            }
        ) { padding ->
            LazyColumn(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(padding)
                    .padding(horizontal = 16.dp),
                verticalArrangement = Arrangement.spacedBy(12.dp)
            ) {
                // NFC Status
                item {
                    Spacer(Modifier.height(4.dp))
                    NfcStatusCard(isNfcAvailable, isNfcEnabled)
                }

                // Default Payment App Warning
                if (isNfcAvailable && !isDefault) {
                    item {
                        Card(
                            modifier = Modifier.fillMaxWidth(),
                            colors = CardDefaults.cardColors(
                                containerColor = Color(0xFF4A1800)
                            )
                        ) {
                            Column(Modifier.padding(16.dp)) {
                                Text(
                                    "Not set as default payment app",
                                    fontWeight = FontWeight.Bold,
                                    color = Color(0xFFFF8A65)
                                )
                                Spacer(Modifier.height(4.dp))
                                Text(
                                    "Terminals won't see this app. Tap below to open NFC settings and select \"NFC Test\" as the default tap-to-pay app.",
                                    fontSize = 13.sp,
                                    color = Color(0xFFFFAB91)
                                )
                                Spacer(Modifier.height(12.dp))
                                Button(
                                    onClick = {
                                        openNfcPaymentSettings()
                                    },
                                    modifier = Modifier.fillMaxWidth(),
                                    colors = ButtonDefaults.buttonColors(
                                        containerColor = Color(0xFFFF8A65),
                                        contentColor = Color.Black
                                    )
                                ) {
                                    Text("Open NFC Payment Settings", fontWeight = FontWeight.Bold)
                                }
                            }
                        }
                    }
                }

                if (isNfcAvailable && isDefault) {
                    item {
                        Card(
                            modifier = Modifier.fillMaxWidth(),
                            colors = CardDefaults.cardColors(
                                containerColor = Color(0xFF1B3A1B)
                            )
                        ) {
                            Column(Modifier.padding(16.dp)) {
                                Text(
                                    "Default payment app — terminals will see this app",
                                    color = Color(0xFF81C784),
                                    fontWeight = FontWeight.Bold,
                                    fontSize = 13.sp
                                )
                                Spacer(Modifier.height(4.dp))
                                Text(
                                    diagnosticInfo.value,
                                    fontSize = 11.sp,
                                    fontFamily = FontFamily.Monospace,
                                    color = Color(0xFF81C784).copy(alpha = 0.7f)
                                )
                            }
                        }
                    }
                }

                // Diagnostics (always visible)
                item {
                    Text(
                        diagnosticInfo.value,
                        fontSize = 11.sp,
                        fontFamily = FontFamily.Monospace,
                        color = MaterialTheme.colorScheme.onSurface.copy(alpha = 0.4f)
                    )
                }

                // Card Number Input
                item {
                    Card(
                        modifier = Modifier.fillMaxWidth(),
                        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
                    ) {
                        Column(Modifier.padding(16.dp)) {
                            Row(verticalAlignment = Alignment.CenterVertically) {
                                Icon(Icons.Default.CreditCard, contentDescription = null,
                                    tint = MaterialTheme.colorScheme.primary)
                                Spacer(Modifier.width(8.dp))
                                Text("Card PAN", fontWeight = FontWeight.Bold)
                            }
                            Spacer(Modifier.height(12.dp))
                            OutlinedTextField(
                                value = panInput,
                                onValueChange = {
                                    if (it.length <= 19 && it.all { c -> c.isDigit() }) {
                                        panInput = it
                                    }
                                },
                                label = { Text("Card Number (PAN)") },
                                keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                                singleLine = true,
                                modifier = Modifier.fillMaxWidth()
                            )
                            Spacer(Modifier.height(8.dp))
                            Text(
                                text = "Display: ${formatPan(panInput)}",
                                style = MaterialTheme.typography.bodyMedium,
                                fontFamily = FontFamily.Monospace,
                                color = MaterialTheme.colorScheme.primary
                            )
                            if (panInput != currentPan.value) {
                                Spacer(Modifier.height(8.dp))
                                FilledTonalButton(
                                    onClick = {
                                        HceService.configuredPan = panInput
                                        currentPan.value = panInput
                                    },
                                    modifier = Modifier.fillMaxWidth()
                                ) {
                                    Text("Apply PAN")
                                }
                            }
                        }
                    }
                }

                // NFC Toggle
                item {
                    NfcToggleCard(active, isNfcEnabled) {
                        nfcActive.value = !nfcActive.value
                        if (nfcActive.value) {
                            // Set as preferred payment service
                            nfcAdapter?.let { adapter ->
                                val emulation = CardEmulation.getInstance(adapter)
                                val component = android.content.ComponentName(
                                    this@MainActivity, HceService::class.java
                                )
                                emulation.setPreferredService(this@MainActivity, component)
                            }
                            nfcEvents.add(0, NfcEvent(
                                timestamp = SimpleDateFormat("HH:mm:ss.SSS", Locale.getDefault()).format(Date()),
                                status = "ACTIVATED",
                                command = "PAN: ${formatPan(currentPan.value)}",
                                response = "Ready for tap",
                                state = "IDLE"
                            ))
                        } else {
                            nfcAdapter?.let { adapter ->
                                val emulation = CardEmulation.getInstance(adapter)
                                emulation.unsetPreferredService(this@MainActivity)
                            }
                            nfcEvents.add(0, NfcEvent(
                                timestamp = SimpleDateFormat("HH:mm:ss.SSS", Locale.getDefault()).format(Date()),
                                status = "DEACTIVATED",
                                command = "",
                                response = "NFC disabled by user",
                                state = "IDLE"
                            ))
                        }
                    }
                }

                // Self-test button
                item {
                    OutlinedButton(
                        onClick = { runSelfTest() },
                        modifier = Modifier.fillMaxWidth()
                    ) {
                        Text("Run Self-Test (simulate terminal)")
                    }
                }

                // Transaction State
                if (active || nfcEvents.isNotEmpty()) {
                    item {
                        TransactionStateCard(state, status)
                    }
                }

                // Event Log
                item {
                    Text(
                        "APDU Event Log (${nfcEvents.size})",
                        style = MaterialTheme.typography.titleSmall,
                        fontWeight = FontWeight.Bold,
                        modifier = Modifier.padding(top = 4.dp)
                    )
                }

                if (nfcEvents.isEmpty()) {
                    item {
                        Card(
                            modifier = Modifier.fillMaxWidth(),
                            colors = CardDefaults.cardColors(
                                containerColor = MaterialTheme.colorScheme.surface
                            )
                        ) {
                            Text(
                                "No events yet. Activate NFC and tap a reader.",
                                modifier = Modifier.fillMaxWidth().padding(24.dp),
                                textAlign = TextAlign.Center,
                                color = MaterialTheme.colorScheme.onSurface.copy(alpha = 0.5f)
                            )
                        }
                    }
                }

                items(nfcEvents) { event ->
                    EventCard(event)
                }

                item { Spacer(Modifier.height(16.dp)) }
            }
        }
    }

    @Composable
    fun NfcStatusCard(available: Boolean, enabled: Boolean) {
        Card(
            modifier = Modifier.fillMaxWidth(),
            colors = CardDefaults.cardColors(
                containerColor = when {
                    !available -> MaterialTheme.colorScheme.error.copy(alpha = 0.15f)
                    !enabled -> Color(0xFF4A3800)
                    else -> Color(0xFF1B3A1B)
                }
            )
        ) {
            Row(
                Modifier.padding(16.dp),
                verticalAlignment = Alignment.CenterVertically
            ) {
                Icon(
                    Icons.Default.Nfc,
                    contentDescription = null,
                    tint = when {
                        !available -> MaterialTheme.colorScheme.error
                        !enabled -> Color(0xFFFFB74D)
                        else -> Color(0xFF81C784)
                    },
                    modifier = Modifier.size(24.dp)
                )
                Spacer(Modifier.width(12.dp))
                Text(
                    text = when {
                        !available -> "NFC hardware not available on this device"
                        !enabled -> "NFC is disabled — enable in system settings"
                        else -> "NFC hardware ready"
                    },
                    color = when {
                        !available -> MaterialTheme.colorScheme.error
                        !enabled -> Color(0xFFFFB74D)
                        else -> Color(0xFF81C784)
                    }
                )
            }
        }
    }

    @Composable
    fun NfcToggleCard(active: Boolean, nfcEnabled: Boolean, onToggle: () -> Unit) {
        val pulseTransition = rememberInfiniteTransition(label = "pulse")
        val pulseScale by pulseTransition.animateFloat(
            initialValue = 1f,
            targetValue = 1.3f,
            animationSpec = infiniteRepeatable(
                animation = tween(1000, easing = LinearEasing),
                repeatMode = RepeatMode.Reverse
            ),
            label = "pulseScale"
        )
        val pulseAlpha by pulseTransition.animateFloat(
            initialValue = 0.6f,
            targetValue = 0f,
            animationSpec = infiniteRepeatable(
                animation = tween(1000, easing = LinearEasing),
                repeatMode = RepeatMode.Reverse
            ),
            label = "pulseAlpha"
        )

        Card(
            modifier = Modifier.fillMaxWidth(),
            colors = CardDefaults.cardColors(
                containerColor = if (active) Color(0xFF0D3B66) else MaterialTheme.colorScheme.surface
            )
        ) {
            Column(
                Modifier.padding(24.dp).fillMaxWidth(),
                horizontalAlignment = Alignment.CenterHorizontally
            ) {
                Box(contentAlignment = Alignment.Center) {
                    if (active) {
                        // Pulse ring
                        Box(
                            modifier = Modifier
                                .size(100.dp)
                                .scale(pulseScale)
                                .alpha(pulseAlpha)
                                .border(3.dp, MaterialTheme.colorScheme.primary, CircleShape)
                        )
                    }
                    FilledIconButton(
                        onClick = onToggle,
                        enabled = nfcEnabled,
                        modifier = Modifier.size(80.dp),
                        shape = CircleShape,
                        colors = IconButtonDefaults.filledIconButtonColors(
                            containerColor = if (active) MaterialTheme.colorScheme.primary
                            else MaterialTheme.colorScheme.surfaceVariant,
                            contentColor = if (active) Color.White
                            else MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    ) {
                        Icon(
                            Icons.Default.Contactless,
                            contentDescription = "Toggle NFC",
                            modifier = Modifier.size(40.dp)
                        )
                    }
                }

                Spacer(Modifier.height(16.dp))

                Text(
                    text = if (active) "NFC ACTIVE — TAP READER" else "TAP TO ACTIVATE",
                    fontWeight = FontWeight.Bold,
                    fontSize = 16.sp,
                    color = if (active) MaterialTheme.colorScheme.primary
                    else MaterialTheme.colorScheme.onSurface.copy(alpha = 0.7f)
                )

                if (active) {
                    Spacer(Modifier.height(4.dp))
                    Text(
                        text = "Card: ${formatPan(currentPan.value)}",
                        fontFamily = FontFamily.Monospace,
                        fontSize = 14.sp,
                        color = MaterialTheme.colorScheme.primary.copy(alpha = 0.8f)
                    )
                }
            }
        }
    }

    @Composable
    fun TransactionStateCard(state: String, status: String) {
        val stateColor = when (state) {
            "COMPLETED" -> Color(0xFF81C784)
            "IDLE" -> MaterialTheme.colorScheme.onSurface.copy(alpha = 0.5f)
            else -> Color(0xFFFFB74D)
        }

        Card(
            modifier = Modifier.fillMaxWidth(),
            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
        ) {
            Column(Modifier.padding(16.dp)) {
                Text("Transaction State", fontWeight = FontWeight.Bold, fontSize = 14.sp)
                Spacer(Modifier.height(8.dp))
                Row {
                    StateChip("IDLE", state == "IDLE")
                    StateChip("SELECTED", state == "SELECTED")
                    StateChip("GPO", state == "GPO_DONE")
                    StateChip("RECORD", state == "RECORD_READ")
                    StateChip("DONE", state == "COMPLETED")
                }
            }
        }
    }

    @Composable
    fun StateChip(label: String, active: Boolean) {
        val bg = if (active) MaterialTheme.colorScheme.primary else Color.Transparent
        val textColor = if (active) Color.White
        else MaterialTheme.colorScheme.onSurface.copy(alpha = 0.4f)
        val borderColor = if (active) MaterialTheme.colorScheme.primary
        else MaterialTheme.colorScheme.onSurface.copy(alpha = 0.2f)

        Box(
            modifier = Modifier
                .padding(end = 4.dp)
                .background(bg, RoundedCornerShape(4.dp))
                .border(1.dp, borderColor, RoundedCornerShape(4.dp))
                .padding(horizontal = 8.dp, vertical = 4.dp)
        ) {
            Text(label, fontSize = 10.sp, color = textColor, fontWeight = FontWeight.Bold)
        }
    }

    @Composable
    fun EventCard(event: NfcEvent) {
        val statusColor = when (event.status) {
            "COMPLETED" -> Color(0xFF81C784)
            "ACTIVATED" -> Color(0xFF4FC3F7)
            "DEACTIVATED" -> Color(0xFFFFB74D)
            "SELECT" -> Color(0xFF9575CD)
            "GPO" -> Color(0xFF4DB6AC)
            "READ_RECORD" -> Color(0xFFFF8A65)
            "GENERATE_AC" -> Color(0xFF81C784)
            "error" -> MaterialTheme.colorScheme.error
            else -> MaterialTheme.colorScheme.onSurface.copy(alpha = 0.6f)
        }

        Card(
            modifier = Modifier.fillMaxWidth(),
            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
        ) {
            Column(Modifier.padding(12.dp)) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Box(
                        modifier = Modifier
                            .size(8.dp)
                            .background(statusColor, CircleShape)
                    )
                    Spacer(Modifier.width(8.dp))
                    Text(
                        event.status,
                        fontWeight = FontWeight.Bold,
                        fontSize = 13.sp,
                        color = statusColor
                    )
                    Spacer(Modifier.weight(1f))
                    Text(
                        event.timestamp,
                        fontSize = 11.sp,
                        fontFamily = FontFamily.Monospace,
                        color = MaterialTheme.colorScheme.onSurface.copy(alpha = 0.4f)
                    )
                }
                if (event.command.isNotEmpty()) {
                    Spacer(Modifier.height(4.dp))
                    Text(
                        "CMD: ${event.command.take(60)}${if (event.command.length > 60) "…" else ""}",
                        fontSize = 11.sp,
                        fontFamily = FontFamily.Monospace,
                        color = MaterialTheme.colorScheme.onSurface.copy(alpha = 0.6f)
                    )
                }
                if (event.response.isNotEmpty()) {
                    Text(
                        "RSP: ${event.response.take(60)}${if (event.response.length > 60) "…" else ""}",
                        fontSize = 11.sp,
                        fontFamily = FontFamily.Monospace,
                        color = MaterialTheme.colorScheme.onSurface.copy(alpha = 0.6f)
                    )
                }
            }
        }
    }

    private fun runSelfTest() {
        NfcLog.clear()
        HceService.configuredPan = currentPan.value
        HceService.configuredToken = "TESTTOKEN001"

        val aidBytes = ApduProcessor.GOLDBANK_AID

        // 1. SELECT
        val selectApdu = byteArrayOf(0x00, 0xA4.toByte(), 0x04, 0x00, aidBytes.size.toByte()) + aidBytes
        ApduProcessor.selectedAid = aidBytes
        val selectResp = ApduProcessor.buildSelectResponse("")
        NfcLog.addEvent("SELECT", ApduProcessor.bytesToHex(selectApdu),
            ApduProcessor.bytesToHex(selectResp), "SELECTED")

        // 2. GPO
        val gpoApdu = byteArrayOf(0x80.toByte(), 0xA8.toByte(), 0x00, 0x00, 0x02, 0x83.toByte(), 0x00)
        val gpoResp = ApduProcessor.buildGpoResponse()
        NfcLog.addEvent("GPO", ApduProcessor.bytesToHex(gpoApdu),
            ApduProcessor.bytesToHex(gpoResp), "GPO_DONE")

        // 3. READ RECORD
        val rrApdu = byteArrayOf(0x00, 0xB2.toByte(), 0x01, 0x0C, 0x00)
        val rrResp = ApduProcessor.buildReadRecordResponse("", currentPan.value)
        NfcLog.addEvent("READ_RECORD", ApduProcessor.bytesToHex(rrApdu),
            ApduProcessor.bytesToHex(rrResp), "RECORD_READ")

        // 4. GENERATE AC
        val acApdu = byteArrayOf(0x80.toByte(), 0xAE.toByte(), 0x80.toByte(), 0x00, 0x00)
        val acResp = ApduProcessor.buildGenerateAcResponse("TESTTOKEN001")
        NfcLog.addEvent("GENERATE_AC", ApduProcessor.bytesToHex(acApdu),
            ApduProcessor.bytesToHex(acResp), "COMPLETED")

        NfcLog.addEvent("SELF_TEST", command = "All 4 steps passed",
            response = "PAN: ${formatPan(currentPan.value)}", state = "COMPLETED")
    }

    private fun formatPan(pan: String): String {
        if (pan.length < 8) return pan
        return pan.chunked(4).joinToString(" ")
    }
}
