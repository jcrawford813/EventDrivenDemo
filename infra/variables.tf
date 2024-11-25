#Top Level Variables
variable "region" {
    type = string
    description = "Azure Region into which resources will be deployed."
    default = "EastUS2"
}

variable "subscription_id" {
    type = string
    description = "Azure Subscription Id"
    default = "0000000-0000-0000-000000"
}

#Tag Values for Resources

variable "tag_author" {
    type = string
    description = "Author tag to add to resources."
    default = "jc"
}

variable "tag_environment" {
    type = string
    description = "The environment tag of the deployment."
    default = "Demo"
}

variable "tag_source" {
    type = string
    description = "The source of the infrastructure being provisioned."
    default = "terraform"
}

variable "resource_group_name" {
    type = string
    description = "The name of the resource group and base name for all resources."
    default = "eventdrivendemo"
}