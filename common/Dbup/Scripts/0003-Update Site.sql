
ALTER TABLE public."Site"
    ADD COLUMN uses_compression boolean NOT NULL DEFAULT false;
ALTER TABLE public."Site"
    ADD COLUMN uses_encryption boolean NOT NULL DEFAULT false;