package com.goldbank.app

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Scaffold
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.Modifier
import com.goldbank.app.navigation.AppNavGraph
import com.goldbank.app.ui.theme.GoldBankTheme
import com.goldbank.app.viewmodel.BrandingViewModel
import org.koin.androidx.viewmodel.ext.android.viewModel

class MainActivity : ComponentActivity() {

    private val brandingViewModel: BrandingViewModel by viewModel()

    override fun onCreate(savedInstanceState: Bundle?) {
        // Swap the launch splash theme for the main app theme before the first frame.
        setTheme(R.style.Theme_GoldBank)
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        setContent {
            val branding by brandingViewModel.branding.collectAsState()
            GoldBankTheme(branding = branding) {
                Scaffold(modifier = Modifier.fillMaxSize()) { innerPadding ->
                    AppNavGraph(modifier = Modifier.padding(innerPadding))
                }
            }
        }
    }
}
