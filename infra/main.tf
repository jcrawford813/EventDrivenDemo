#Initialize Locals and Resources
resource "random_string" "random_suffix" {
    length = 6
    lower = true
    upper = false
    numeric = true
    special = false
}

locals {
    suffix = random_string.random_suffix.result
}

data "azurerm_client_config" "current" { } 

#Create / Setup Resource Group
resource "azurerm_resource_group" "demo_rg" {
  location = var.region
  name     = "${var.resource_group_name}-${local.suffix}"
  tags = {
    author = var.tag_author
    environment = var.tag_environment
    source = var.tag_source
  }
}

#Create / Setup Key Vault
resource "azurerm_key_vault" "demo_kv" {
  location = var.region
  name = "demokv-${local.suffix}"
  resource_group_name = azurerm_resource_group.demo_rg.name
  tenant_id = data.azurerm_client_config.current.tenant_id
  sku_name = "standard"

  tags = {
    author = var.tag_author
    environment = var.tag_environment
    source = var.tag_source
  }

  #Give Current User Full Access to KV
  access_policy {
    tenant_id = data.azurerm_client_config.current.tenant_id
    object_id = data.azurerm_client_config.current.object_id

    key_permissions = [
      "Get",
      "Create",
      "Delete",
      "List",
      "Recover",
      "Restore",
      "UnwrapKey",
      "WrapKey",
      "List"
    ]

    secret_permissions = [
      "Get",
      "List",
      "Set",
      "Delete",
      "Recover",
      "Restore",
      "Purge"
    ]
  }
}

#Create / Setup Service Bus
resource "azurerm_servicebus_namespace" "demo_sb" {
  location = var.region
  resource_group_name = azurerm_resource_group.demo_rg.name
  name = "${var.resource_group_name}-sb-${local.suffix}"
  sku = "Standard"

  tags = {
    author = var.tag_author
    environment = var.tag_environment
    source = var.tag_source
  }
}

#Create / Setup Topics
resource "azurerm_servicebus_topic" "demo_sb_topic_lineprocess" {
  name = "line-extracted"
  namespace_id = azurerm_servicebus_namespace.demo_sb.id
}

resource "azurerm_servicebus_topic" "demo_sb_topic_filebuild" {
  name = "line-processed"
  namespace_id = azurerm_servicebus_namespace.demo_sb.id
}

resource "azurerm_servicebus_topic" "demo_sb_topic_filecompleted" {
  name = "file-completed"
  namespace_id = azurerm_servicebus_namespace.demo_sb.id
}


#Add Connection String for SB into KeyVault
resource "azurerm_key_vault_secret" "demo_kv_sb_connection" {
  name = "serviceBusConnectionString"
  key_vault_id = azurerm_key_vault.demo_kv.id
  value = azurerm_servicebus_namespace.demo_sb.default_primary_connection_string
}

#Create Azure Storage for Files and Tables
resource "azurerm_storage_account" "demo_storage" {
  name                     = "demostorage${local.suffix}"
  resource_group_name      = azurerm_resource_group.demo_rg.name
  location                 = var.region
  account_tier             = "Standard"
  account_replication_type = "LRS"

  tags = {
    author = var.tag_author
    environment = var.tag_environment
    source = var.tag_source
  }
}

resource "azurerm_storage_container" "demo_storage_files" {
  name = "demo-files"
  container_access_type = "private"
  storage_account_name = azurerm_storage_account.demo_storage.name
}

resource "azurerm_storage_table" "demo_storage_tables" {
  name = "demoTable"
  storage_account_name = azurerm_storage_account.demo_storage.name
}

#Add Storage Connection String to Key Vault
resource "azurerm_key_vault_secret" "demo_kv_storage_files_connection" {
  name = "fileStorageConnectionString"
  key_vault_id = azurerm_key_vault.demo_kv.id
  value = azurerm_storage_account.demo_storage.primary_connection_string
}

#Create Function App Service
resource "azurerm_storage_account" "demo_function_storage" {
  name = "demofuncstorage${local.suffix}"
  resource_group_name = azurerm_resource_group.demo_rg.name
  location = var.region
  account_tier = "Standard"
  account_replication_type = "LRS"

  tags = {
    author = var.tag_author
    environment = var.tag_environment
    source = var.tag_source
  }
}

resource "azurerm_service_plan" "demo_function_app_service" {
  name = "${var.resource_group_name}-functions-${local.suffix}"
  resource_group_name = azurerm_resource_group.demo_rg.name
  location = var.region
  os_type = "Linux"
  sku_name = "S1"

  tags = {
    author = var.tag_author
    environment = var.tag_environment
    source = var.tag_source
  }
}

resource "azurerm_linux_function_app" "demo_function_app" {
  name = "${var.resource_group_name}-function-app"
  resource_group_name = azurerm_resource_group.demo_rg.name
  location = var.region
  storage_account_name = azurerm_storage_account.demo_function_storage.name
  storage_account_access_key = azurerm_storage_account.demo_function_storage.primary_access_key
  service_plan_id = azurerm_service_plan.demo_function_app_service.id
  depends_on = [ azurerm_service_plan.demo_function_app_service , azurerm_storage_account.demo_function_storage ]
  site_config {
    use_32_bit_worker = false
  }
  
  identity {
    type = "SystemAssigned"
  }

  app_settings = {
    "ServiceBusConnectionString" = "@Microsoft.KeyVault(SecretUri=${azurerm_key_vault_secret.demo_kv_sb_connection.id})"
    "StorageConnectionString" = "@Microsoft.KeyVault(SecretUri=${azurerm_key_vault_secret.demo_kv_storage_files_connection.id})"
  }
}

#Give Function Identity Access to Key Vault
resource "azurerm_key_vault_access_policy" "demo_function_app_key_access" {
  key_vault_id = azurerm_key_vault.demo_kv.id
  tenant_id    = data.azurerm_client_config.current.tenant_id
  object_id    = azurerm_linux_function_app.demo_function_app.identity[0].principal_id

  secret_permissions = ["Get", "List"]
}

#Create Web App
resource "azurerm_service_plan" "demo_web_app_service_plan" {
  name                = "demoapp-${local.suffix}"
  location            = var.region
  resource_group_name = azurerm_resource_group.demo_rg.name
  os_type             = "Linux"
  sku_name            = "B1"

  tags = {
    author = var.tag_author
    environment = var.tag_environment
    source = var.tag_source
  }
}

resource "azurerm_linux_web_app" "demo_web_app" {
  name                  = "${var.resource_group_name}-web-${local.suffix}"
  location              = var.region
  resource_group_name   = azurerm_resource_group.demo_rg.name
  service_plan_id       = azurerm_service_plan.demo_web_app_service_plan.id
  https_only            = true
  site_config { 
    minimum_tls_version = "1.2"
  }

  app_settings = {
    "ServiceBusConnectionString" = azurerm_servicebus_namespace.demo_sb.default_primary_connection_string
    "StorageConnectionString" = azurerm_storage_account.demo_storage.primary_connection_string
  }

  tags = {
    author = var.tag_author
    environment = var.tag_environment
    source = var.tag_source
  }
}