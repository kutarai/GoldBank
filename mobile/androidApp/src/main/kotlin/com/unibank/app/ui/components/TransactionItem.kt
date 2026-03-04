package com.unibank.app.ui.components

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.CallMade
import androidx.compose.material.icons.automirrored.filled.CallReceived
import androidx.compose.material.icons.filled.AccountBalance
import androidx.compose.material.icons.filled.Contactless
import androidx.compose.material.icons.filled.CurrencyExchange
import androidx.compose.material.icons.filled.Loop
import androidx.compose.material.icons.filled.Payments
import androidx.compose.material.icons.filled.QrCode
import androidx.compose.material.icons.filled.Receipt
import androidx.compose.material.icons.filled.SwapHoriz
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import com.unibank.shared.domain.model.Transaction
import com.unibank.shared.domain.model.TransactionType
import com.unibank.shared.domain.util.MoneyFormatter

@Composable
fun TransactionItem(
    transaction: Transaction,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
) {
    val (icon, tint) = transaction.type.iconAndTint()
    val isCredit = transaction.type in listOf(
        TransactionType.CASH_IN,
        TransactionType.P2P_RECEIVE,
        TransactionType.REVERSAL,
    )
    val amountColor = if (isCredit) Color(0xFF2E7D32) else MaterialTheme.colorScheme.onSurface

    Row(
        modifier = modifier
            .fillMaxWidth()
            .clickable(onClick = onClick)
            .padding(horizontal = 16.dp, vertical = 12.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Icon(
            imageVector = icon,
            contentDescription = null,
            tint = tint,
            modifier = Modifier.size(36.dp),
        )
        Spacer(modifier = Modifier.width(12.dp))
        Column(modifier = Modifier.weight(1f)) {
            Text(
                text = transaction.description.ifEmpty {
                    transaction.counterpartyName.ifEmpty { transaction.type.displayName() }
                },
                style = MaterialTheme.typography.bodyMedium,
                fontWeight = FontWeight.Medium,
                maxLines = 1,
            )
            Spacer(modifier = Modifier.height(2.dp))
            Text(
                text = transaction.type.displayName(),
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
        Spacer(modifier = Modifier.width(8.dp))
        Column(horizontalAlignment = Alignment.End) {
            Text(
                text = "${if (isCredit) "+" else "-"}${MoneyFormatter.format(transaction.amount.amount, transaction.amount.currency)}",
                style = MaterialTheme.typography.bodyMedium,
                fontWeight = FontWeight.SemiBold,
                color = amountColor,
            )
            if (transaction.status.name != "COMPLETED") {
                Text(
                    text = transaction.status.name.lowercase().replaceFirstChar { it.uppercase() },
                    style = MaterialTheme.typography.labelSmall,
                    color = MaterialTheme.colorScheme.tertiary,
                )
            }
        }
    }
}

@Composable
private fun TransactionType.iconAndTint(): Pair<ImageVector, Color> = when (this) {
    TransactionType.CASH_IN -> Icons.AutoMirrored.Filled.CallReceived to Color(0xFF2E7D32)
    TransactionType.CASH_OUT -> Icons.AutoMirrored.Filled.CallMade to Color(0xFFC62828)
    TransactionType.P2P_SEND -> Icons.Default.Payments to Color(0xFFE65100)
    TransactionType.P2P_RECEIVE -> Icons.Default.Payments to Color(0xFF2E7D32)
    TransactionType.PAYMENT_NFC -> Icons.Default.Contactless to Color(0xFF1565C0)
    TransactionType.PAYMENT_QR -> Icons.Default.QrCode to Color(0xFF1565C0)
    TransactionType.BILL_PAYMENT -> Icons.Default.Receipt to Color(0xFF6A1B9A)
    TransactionType.TRANSFER_DOMESTIC -> Icons.Default.SwapHoriz to Color(0xFF00695C)
    TransactionType.TRANSFER_CROSS_BORDER -> Icons.Default.CurrencyExchange to Color(0xFF00695C)
    TransactionType.FEE -> Icons.Default.AccountBalance to Color(0xFF757575)
    TransactionType.REVERSAL -> Icons.Default.Loop to Color(0xFF2E7D32)
    TransactionType.SETTLEMENT -> Icons.Default.AccountBalance to Color(0xFF37474F)
    TransactionType.UNSPECIFIED -> Icons.Default.SwapHoriz to Color(0xFF757575)
}

private fun TransactionType.displayName(): String = when (this) {
    TransactionType.CASH_IN -> "Cash In"
    TransactionType.CASH_OUT -> "Cash Out"
    TransactionType.P2P_SEND -> "Sent Money"
    TransactionType.P2P_RECEIVE -> "Received Money"
    TransactionType.PAYMENT_NFC -> "NFC Payment"
    TransactionType.PAYMENT_QR -> "QR Payment"
    TransactionType.BILL_PAYMENT -> "Bill Payment"
    TransactionType.TRANSFER_DOMESTIC -> "Domestic Transfer"
    TransactionType.TRANSFER_CROSS_BORDER -> "Cross-Border Transfer"
    TransactionType.FEE -> "Fee"
    TransactionType.REVERSAL -> "Reversal"
    TransactionType.SETTLEMENT -> "Settlement"
    TransactionType.UNSPECIFIED -> "Transaction"
}
