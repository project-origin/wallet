# Certificate attributes

To read about the concept of attributes, see [Attributes](https://project-origin.github.io/registry/concept/granular-certificates/attributes.html).

Attributes on certificates can currently be split into two categories, Cleartext attributes and Hashed attributes.

## Cleartext attributes

Cleartext attributes are stored on the certificate in cleartext and everyone can read them.

A wallet will automatically load all cleartext attributes from the certificate and store them in the wallet when certificate is received.

## Hashed attributes

Hashed attributes are stored on the certificate as a hash, and only those who have been given the value and salt can verify that the hash is correct.
These salts and values are stored in the wallet, and can be shared with other wallets when transferring a certificate, if the current owner wants to.

The hash are calculated as follows: `sha256(key|value|certificateId|salt)`

### Sending hashed attributes

To send a certificate with hashed attributes,
the keys of the attributes simple needs to be added to the `hashed_attributes` array in the TransferRequest.

### Receiving hashed attributes

When receiving a certificate with hashed attributes,
the wallet will automatically store the salt and value of the attribute in the wallet if valid,
otherwise the attribute will be ignored.
