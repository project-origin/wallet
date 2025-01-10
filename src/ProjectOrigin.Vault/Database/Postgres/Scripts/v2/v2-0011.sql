CREATE INDEX IF NOT EXISTS idx_wallet_slices_certificate_id_registry
    ON public.wallet_slices 
    (certificate_id, registry_name);

CREATE INDEX IF NOT EXISTS idx_attributes_certificate_id_registry
    ON public.attributes
    (certificate_id, attribute_key, registry_name);

CREATE INDEX IF NOT EXISTS idx_wallet_attributes_certificate_wallet_attribute
    ON public.wallet_attributes
    (certificate_id, wallet_id, registry_name);

CREATE INDEX IF NOT EXISTS idx_wallet_slices_wallet_endpoint_id
    ON public.wallet_slices
    (wallet_endpoint_id);
