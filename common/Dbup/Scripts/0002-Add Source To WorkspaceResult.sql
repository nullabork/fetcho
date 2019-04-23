-- template file, copy this for all future files
--
--
--

ALTER TABLE public."WorkspaceResult" ADD COLUMN source_server_id uuid;
UPDATE public."WorkspaceResult" set source_server_id = '8c164ff4-4779-41ea-86bc-5a89d8aa71e4';
ALTER TABLE public."WorkspaceResult" ALTER COLUMN source_server_id SET NOT NULL;

alter table public."WorkspaceResult" add column features text[];
alter table public."WorkspaceResult" drop column debug_info;
