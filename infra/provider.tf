terraform {
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "=3.16.0"
    }
    random = {
        source = "hashicorp/random"
        version = "3.6.3"
    }
  }
}

#Initialize Provider for Azure
provider "azurerm" {
    features {
      
    }
}