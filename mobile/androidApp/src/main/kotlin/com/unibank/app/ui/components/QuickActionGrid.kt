package com.unibank.app.ui.components

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.Send
import androidx.compose.material.icons.filled.Contactless
import androidx.compose.material.icons.filled.AccountBalanceWallet
import androidx.compose.material.icons.filled.CreditScore
import androidx.compose.material.icons.filled.LocalAtm
import androidx.compose.material.icons.filled.AddCard
import androidx.compose.material.icons.filled.Diamond
import androidx.compose.material.icons.filled.QrCode2
import androidx.compose.material.icons.filled.Receipt
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp

data class QuickAction(
    val label: String,
    val icon: ImageVector,
    val routeKey: String,
)

val defaultQuickActions = listOf(
    QuickAction("Send", Icons.AutoMirrored.Filled.Send, "p2p_transfer"),
    QuickAction("Pay QR", Icons.Default.QrCode2, "qr_scan"),
    QuickAction("Pay NFC", Icons.Default.Contactless, "nfc_payment"),
    QuickAction("Bills", Icons.Default.Receipt, "bill_pay"),
    QuickAction("Cash In", Icons.Default.AccountBalanceWallet, "cash_in"),
    QuickAction("Cash Out", Icons.Default.LocalAtm, "cash_out"),
    QuickAction("My QR", Icons.Default.QrCode2, "qr_generate"),
    QuickAction("Loan", Icons.Default.CreditScore, "loan"),
    QuickAction("Deposit Cheque", Icons.Default.AddCard, "cheque_deposit"),
    QuickAction("Assets", Icons.Default.Diamond, "assets"),
)

@Composable
fun QuickActionGrid(
    actions: List<QuickAction> = defaultQuickActions,
    onActionClick: (String) -> Unit,
    modifier: Modifier = Modifier,
) {
    Column(
        modifier = modifier.fillMaxWidth(),
        verticalArrangement = Arrangement.spacedBy(8.dp),
    ) {
        actions.chunked(3).forEach { rowItems ->
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(8.dp),
            ) {
                rowItems.forEach { action ->
                    Surface(
                        modifier = Modifier
                            .weight(1f)
                            .clickable { onActionClick(action.routeKey) },
                        shape = MaterialTheme.shapes.medium,
                        color = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.5f),
                    ) {
                        Column(
                            modifier = Modifier.padding(12.dp),
                            horizontalAlignment = Alignment.CenterHorizontally,
                        ) {
                            Icon(
                                imageVector = action.icon,
                                contentDescription = action.label,
                                modifier = Modifier.size(28.dp),
                                tint = MaterialTheme.colorScheme.primary,
                            )
                            Text(
                                text = action.label,
                                style = MaterialTheme.typography.labelSmall,
                                textAlign = TextAlign.Center,
                                modifier = Modifier.padding(top = 4.dp),
                            )
                        }
                    }
                }
                // Fill remaining space if row has fewer than 3 items
                repeat(3 - rowItems.size) {
                    androidx.compose.foundation.layout.Spacer(modifier = Modifier.weight(1f))
                }
            }
        }
    }
}
