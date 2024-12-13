# Expire
Certificates older than configured amount of days are expired. That means that the certificate cannot be slices, transferred, allocated or claimed. The certificate can still be withdrawn and unclaimed. A certificate can be issued after the expiry date (but stille cannot be used for the above).

Expire works by having the registry and verifyer checking if the end date of a certificate is past the expiry date. Furthermore a background job in Vault runs a stored procedure each hour (this is configurable), which sets all available slice states to expired, if end date is after expiry date.

Currently in Energy Track and Trace DK a certificate has been configured to expire after 60 days.

If the expiry days environment parameter is not set, expiry does not run in Vault and the Verifyer does not check for expiry.
