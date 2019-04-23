--
-- PostgreSQL database dump
--

-- Dumped from database version 11.2
-- Dumped by pg_dump version 11.2

-- Started on 2019-04-23 20:18:46

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET client_min_messages = warning;
SET row_security = off;

SET default_with_oids = false;

--
-- TOC entry 196 (class 1259 OID 16385)
-- Name: Account; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Account" (
    name text NOT NULL,
    created timestamp with time zone NOT NULL,
    is_active boolean DEFAULT true NOT NULL
);


--
-- TOC entry 206 (class 1259 OID 632145)
-- Name: AccountProperty; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."AccountProperty" (
    account_name text NOT NULL,
    name text NOT NULL,
    value text,
    created timestamp without time zone NOT NULL,
    id bigint NOT NULL
)
WITH (fillfactor='95');


--
-- TOC entry 205 (class 1259 OID 632143)
-- Name: AccountProperty_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public."AccountProperty_id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- TOC entry 2246 (class 0 OID 0)
-- Dependencies: 205
-- Name: AccountProperty_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public."AccountProperty_id_seq" OWNED BY public."AccountProperty".id;


--
-- TOC entry 209 (class 1259 OID 1367149)
-- Name: Server; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Server" (
    name text NOT NULL,
    server_id uuid NOT NULL,
    min_hash bytea NOT NULL,
    max_hash bytea NOT NULL,
    approved boolean NOT NULL,
    created timestamp without time zone NOT NULL
);


--
-- TOC entry 197 (class 1259 OID 16392)
-- Name: Site; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Site" (
    hostname text,
    robots_file bytea,
    last_robots_fetched timestamp with time zone,
    is_blocked boolean,
    hostname_hash bytea,
    uses_compression boolean DEFAULT false NOT NULL,
    uses_encryption boolean DEFAULT false NOT NULL
);


--
-- TOC entry 198 (class 1259 OID 16398)
-- Name: WebResource; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."WebResource" (
    next_fetch timestamp without time zone,
    urihash bytea
)
WITH (fillfactor='95', autovacuum_enabled='false');


--
-- TOC entry 203 (class 1259 OID 163508)
-- Name: WebResourceDataCache; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."WebResourceDataCache" (
    datahash bytea NOT NULL,
    data bytea NOT NULL
);


--
-- TOC entry 199 (class 1259 OID 16404)
-- Name: Workspace; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Workspace" (
    workspace_id uuid NOT NULL,
    name text NOT NULL,
    created timestamp with time zone NOT NULL,
    description text,
    is_active boolean DEFAULT true NOT NULL,
    query_text text DEFAULT ''::text NOT NULL,
    result_count bigint DEFAULT 0 NOT NULL,
    is_wellknown boolean DEFAULT false NOT NULL,
    revision integer DEFAULT 0
);


--
-- TOC entry 200 (class 1259 OID 16414)
-- Name: WorkspaceAccessKey; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."WorkspaceAccessKey" (
    workspace_id uuid NOT NULL,
    account_name text NOT NULL,
    created timestamp with time zone NOT NULL,
    expiry timestamp with time zone NOT NULL,
    is_active boolean DEFAULT true NOT NULL,
    workspace_access_key_id uuid NOT NULL,
    permissions integer DEFAULT 0 NOT NULL,
    is_wellknown boolean DEFAULT false NOT NULL,
    name text NOT NULL,
    revision integer DEFAULT 0 NOT NULL
);


--
-- TOC entry 204 (class 1259 OID 258847)
-- Name: WorkspaceAccessKey_revision_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public."WorkspaceAccessKey_revision_seq"
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- TOC entry 2247 (class 0 OID 0)
-- Dependencies: 204
-- Name: WorkspaceAccessKey_revision_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public."WorkspaceAccessKey_revision_seq" OWNED BY public."WorkspaceAccessKey".revision;


--
-- TOC entry 207 (class 1259 OID 632154)
-- Name: WorkspaceQueryStats; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."WorkspaceQueryStats" (
    workspace_id uuid NOT NULL,
    max_cost bigint,
    avg_cost bigint,
    total_cost bigint,
    eval_count bigint,
    include_count bigint,
    exclude_count bigint,
    tag_count bigint,
    created timestamp without time zone,
    sequence bigint NOT NULL
);


--
-- TOC entry 208 (class 1259 OID 632166)
-- Name: WorkspaceQueryStats_sequence_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public."WorkspaceQueryStats_sequence_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- TOC entry 2248 (class 0 OID 0)
-- Dependencies: 208
-- Name: WorkspaceQueryStats_sequence_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public."WorkspaceQueryStats_sequence_seq" OWNED BY public."WorkspaceQueryStats".sequence;


--
-- TOC entry 201 (class 1259 OID 16423)
-- Name: WorkspaceResult; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."WorkspaceResult" (
    urihash bytea NOT NULL,
    uri text NOT NULL,
    referer text,
    title text,
    description text,
    created timestamp with time zone NOT NULL,
    workspace_id uuid NOT NULL,
    sequence bigint NOT NULL,
    page_size bigint DEFAULT 0,
    random double precision DEFAULT random() NOT NULL,
    tags text,
    datahash bytea,
    updated timestamp with time zone,
    debug_info text
)
WITH (fillfactor='95');


--
-- TOC entry 202 (class 1259 OID 16431)
-- Name: WorkspaceResult_sequence_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public."WorkspaceResult_sequence_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- TOC entry 2249 (class 0 OID 0)
-- Dependencies: 202
-- Name: WorkspaceResult_sequence_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public."WorkspaceResult_sequence_seq" OWNED BY public."WorkspaceResult".sequence;


--
-- TOC entry 2105 (class 2604 OID 632148)
-- Name: AccountProperty id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AccountProperty" ALTER COLUMN id SET DEFAULT nextval('public."AccountProperty_id_seq"'::regclass);


--
-- TOC entry 2106 (class 2604 OID 632168)
-- Name: WorkspaceQueryStats sequence; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."WorkspaceQueryStats" ALTER COLUMN sequence SET DEFAULT nextval('public."WorkspaceQueryStats_sequence_seq"'::regclass);


--
-- TOC entry 2104 (class 2604 OID 16433)
-- Name: WorkspaceResult sequence; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."WorkspaceResult" ALTER COLUMN sequence SET DEFAULT nextval('public."WorkspaceResult_sequence_seq"'::regclass);


--
-- TOC entry 2119 (class 2606 OID 632153)
-- Name: AccountProperty PK_AccountProperty; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AccountProperty"
    ADD CONSTRAINT "PK_AccountProperty" PRIMARY KEY (id) WITH (fillfactor='95');


--
-- TOC entry 2116 (class 2606 OID 16435)
-- Name: WorkspaceResult PK_WorkspaceResult; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."WorkspaceResult"
    ADD CONSTRAINT "PK_WorkspaceResult" PRIMARY KEY (urihash, workspace_id);


--
-- TOC entry 2117 (class 1259 OID 163749)
-- Name: IX_WebResourceDataCache_datahash; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_WebResourceDataCache_datahash" ON public."WebResourceDataCache" USING btree (datahash);


--
-- TOC entry 2111 (class 1259 OID 163719)
-- Name: IX_WorkspaceResult_Workspace; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_WorkspaceResult_Workspace" ON public."WorkspaceResult" USING btree (workspace_id) WITH (fillfactor='95');


--
-- TOC entry 2112 (class 1259 OID 632142)
-- Name: IX_WorkspaceResult_WorkspaceRandom; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_WorkspaceResult_WorkspaceRandom" ON public."WorkspaceResult" USING btree (workspace_id, random);


--
-- TOC entry 2113 (class 1259 OID 632141)
-- Name: IX_WorkspaceResult_WorkspaceSequence; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_WorkspaceResult_WorkspaceSequence" ON public."WorkspaceResult" USING btree (workspace_id, sequence);


--
-- TOC entry 2114 (class 1259 OID 163770)
-- Name: IX_WorkspaceResult_WorkspaceUriHash; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_WorkspaceResult_WorkspaceUriHash" ON public."WorkspaceResult" USING btree (urihash, workspace_id) WITH (fillfactor='95');


--
-- TOC entry 2107 (class 1259 OID 16436)
-- Name: Site_hostname_hash_idx; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "Site_hostname_hash_idx" ON public."Site" USING btree (hostname_hash) WITH (fillfactor='95');


--
-- TOC entry 2108 (class 1259 OID 16437)
-- Name: Site_hostname_idx; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "Site_hostname_idx" ON public."Site" USING btree (hostname);


--
-- TOC entry 2109 (class 1259 OID 16438)
-- Name: WebResource_urihash_data_idx; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "WebResource_urihash_data_idx" ON public."WebResource" USING btree (urihash, next_fetch) WITH (fillfactor='95');


--
-- TOC entry 2110 (class 1259 OID 16439)
-- Name: WebResource_urihash_idx; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "WebResource_urihash_idx" ON public."WebResource" USING btree (urihash) WITH (fillfactor='95');


-- Completed on 2019-04-23 20:18:46

--
-- PostgreSQL database dump complete
--

