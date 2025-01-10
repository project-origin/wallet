CREATE INDEX IF NOT EXISTS idx_wallet_slices_certificate_id_registry
    ON public.wallet_slices 
    (certificate_id, registry_name);

CREATE INDEX IF NOT EXISTS idx_attributes_certificate_id_registry
    ON public.attributes
    (registry_name, certificate_id);

CREATE INDEX IF NOT EXISTS idx_wallet_attributes_certificate_wallet_attribute
    ON public.wallet_attributes
    (registry_name, wallet_id, certificate_id);
