-- =====================================================================
-- 06_staging_worker.sql
-- Área de staging (schema stg) y procedimiento de carga hacia dw.
-- Ejecutar en la base ANALÍTICA (opiniones_clientes) DESPUÉS de 04_modelo_analitico_dw.sql.
-- En DBeaver: ejecutar como script (Alt+X).
-- =====================================================================

CREATE SCHEMA IF NOT EXISTS stg;

-- Opiniones normalizadas de las tres fuentes (una fila por opinión).
CREATE TABLE IF NOT EXISTS stg.opinion (
    origen           VARCHAR(10)  NOT NULL,              -- Encuesta / Web / Social
    id_origen        VARCHAR(20)  NOT NULL,
    id_cliente       INTEGER,                            -- NULL = anónimo
    id_producto      INTEGER      NOT NULL,
    fecha            DATE         NOT NULL,
    comentario       VARCHAR(500) NOT NULL,
    puntaje          SMALLINT,                           -- NULL en redes sociales
    canal            VARCHAR(30)  NOT NULL,              -- EncuestaInterna / Web / Instagram / ...
    sentimiento      VARCHAR(20),                        -- Positiva / Neutra / Negativa
    fecha_extraccion TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT pk_stg_opinion PRIMARY KEY (origen, id_origen),
    CONSTRAINT ck_stg_opinion_origen CHECK (origen IN ('Encuesta', 'Web', 'Social'))
);

-- Dimensiones de referencia leídas de la base operacional.
CREATE TABLE IF NOT EXISTS stg.cliente (
    id_cliente INTEGER      PRIMARY KEY,
    nombre     VARCHAR(100) NOT NULL,
    email      VARCHAR(150)
);

CREATE TABLE IF NOT EXISTS stg.producto (
    id_producto INTEGER      PRIMARY KEY,
    nombre      VARCHAR(100) NOT NULL,
    categoria   VARCHAR(50)  NOT NULL
);

-- Registros rechazados (por validación al extraer o por integridad al cargar).
CREATE TABLE IF NOT EXISTS stg.rechazo (
    id_rechazo         BIGINT       GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    etapa              VARCHAR(15)  NOT NULL,            -- Extraccion / Carga
    origen             VARCHAR(10)  NOT NULL,
    id_registro        VARCHAR(30)  NOT NULL,
    motivo             VARCHAR(500) NOT NULL,
    datos_originales   VARCHAR(1000),
    fecha_rechazo      TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- ---------------------------------------------------------------------
-- stg.sp_cargar_dw: mueve lo que hay en staging hacia dw.
--   * Es idempotente: se puede ejecutar varias veces (UNIQUE origen,id_origen + ON CONFLICT).
--   * Las opiniones que referencian un producto/cliente inexistente NO entran al almacén:
--     se registran en stg.rechazo (etapa 'Carga') y se cuentan en p_rechazados.
--   * Todo ocurre en la transacción del llamador (el Worker abre una).
-- ---------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE stg.sp_cargar_dw(
    INOUT p_en_staging  INTEGER DEFAULT 0,
    INOUT p_dias        INTEGER DEFAULT 0,
    INOUT p_productos   INTEGER DEFAULT 0,
    INOUT p_clientes    INTEGER DEFAULT 0,
    INOUT p_fuentes     INTEGER DEFAULT 0,
    INOUT p_rechazados  INTEGER DEFAULT 0,
    INOUT p_hechos      INTEGER DEFAULT 0)
LANGUAGE plpgsql
AS $$
BEGIN
    SELECT COUNT(*) INTO p_en_staging FROM stg.opinion;

    -- Los rechazos de integridad se recalculan en cada ejecución (los de extracción los maneja el Worker).
    DELETE FROM stg.rechazo WHERE etapa = 'Carga';

    -- Clientes / productos: se actualizan primero porque los hechos dependen de ellos.
    INSERT INTO dw.dim_producto (id_producto, nombre, categoria)
    SELECT id_producto, nombre, categoria FROM stg.producto
    ON CONFLICT (id_producto) DO UPDATE
        SET nombre = EXCLUDED.nombre, categoria = EXCLUDED.categoria;
    GET DIAGNOSTICS p_productos = ROW_COUNT;

    INSERT INTO dw.dim_cliente (id_cliente, nombre, email)
    SELECT id_cliente, nombre, email FROM stg.cliente
    ON CONFLICT (id_cliente) DO UPDATE
        SET nombre = EXCLUDED.nombre, email = EXCLUDED.email;
    GET DIAGNOSTICS p_clientes = ROW_COUNT;

    -- Rechazos de integridad: producto o cliente (si viene informado) inexistentes en el almacén.
    INSERT INTO stg.rechazo (etapa, origen, id_registro, motivo, datos_originales)
    SELECT 'Carga', o.origen, o.id_origen,
           CASE WHEN p.sk_producto IS NULL
                THEN 'Producto inexistente: ' || o.id_producto
                ELSE 'Cliente inexistente: ' || o.id_cliente END,
           LEFT(o.id_origen || '|' || COALESCE(o.id_cliente::TEXT, '') || '|' || o.id_producto
                || '|' || o.fecha || '|' || o.comentario, 1000)
      FROM stg.opinion o
      LEFT JOIN dw.dim_producto p ON p.id_producto = o.id_producto
      LEFT JOIN dw.dim_cliente  c ON c.id_cliente  = o.id_cliente
     WHERE p.sk_producto IS NULL
        OR (o.id_cliente IS NOT NULL AND c.sk_cliente IS NULL);
    GET DIAGNOSTICS p_rechazados = ROW_COUNT;

    -- Calendario: todos los días de los meses cubiertos por las opiniones.
    INSERT INTO dw.dim_tiempo
        (id_tiempo, fecha, anio, trimestre, mes, nombre_mes, anio_mes, anio_trimestre,
         dia, dia_semana, nombre_dia, es_fin_de_semana)
    SELECT to_char(g.d, 'YYYYMMDD')::INTEGER,
           g.d::DATE,
           EXTRACT(YEAR FROM g.d)::SMALLINT,
           EXTRACT(QUARTER FROM g.d)::SMALLINT,
           EXTRACT(MONTH FROM g.d)::SMALLINT,
           (ARRAY['Enero','Febrero','Marzo','Abril','Mayo','Junio','Julio','Agosto',
                  'Septiembre','Octubre','Noviembre','Diciembre'])[EXTRACT(MONTH FROM g.d)::INT],
           to_char(g.d, 'YYYY-MM'),
           to_char(g.d, 'YYYY') || '-T' || EXTRACT(QUARTER FROM g.d)::INT,
           EXTRACT(DAY FROM g.d)::SMALLINT,
           EXTRACT(ISODOW FROM g.d)::SMALLINT,
           (ARRAY['Lunes','Martes','Miércoles','Jueves','Viernes','Sábado','Domingo'])[EXTRACT(ISODOW FROM g.d)::INT],
           EXTRACT(ISODOW FROM g.d) IN (6, 7)
      FROM generate_series(
               (SELECT date_trunc('month', MIN(fecha))::TIMESTAMP FROM stg.opinion),
               (SELECT (date_trunc('month', MAX(fecha)) + INTERVAL '1 month' - INTERVAL '1 day')::TIMESTAMP FROM stg.opinion),
               INTERVAL '1 day') AS g(d)
    ON CONFLICT (id_tiempo) DO NOTHING;
    GET DIAGNOSTICS p_dias = ROW_COUNT;

    -- Fuentes nuevas (p. ej. una red social que aún no estaba en la dimensión).
    INSERT INTO dw.dim_fuente (tipo_fuente, canal)
    SELECT DISTINCT
           CASE o.origen WHEN 'Encuesta' THEN 'Encuesta' WHEN 'Web' THEN 'Reseña web' ELSE 'Red social' END,
           o.canal
      FROM stg.opinion o
    ON CONFLICT (tipo_fuente, canal) DO NOTHING;
    GET DIAGNOSTICS p_fuentes = ROW_COUNT;

    -- Hechos.
    INSERT INTO dw.fact_opinion
        (id_tiempo, sk_producto, sk_cliente, sk_fuente, sk_sentimiento, sk_evento,
         origen, id_origen, puntaje, es_positiva, es_neutra, es_negativa, comentario)
    SELECT t.id_tiempo,
           p.sk_producto,
           COALESCE(c.sk_cliente, 0),
           f.sk_fuente,
           COALESCE(s.sk_sentimiento, 0),
           COALESCE(ev.sk_evento, 0),
           o.origen,
           o.id_origen,
           o.puntaje,
           CASE WHEN s.nombre = 'Positiva' THEN 1 ELSE 0 END,
           CASE WHEN s.nombre = 'Neutra'   THEN 1 ELSE 0 END,
           CASE WHEN s.nombre = 'Negativa' THEN 1 ELSE 0 END,
           o.comentario
      FROM stg.opinion o
      JOIN dw.dim_tiempo   t ON t.fecha = o.fecha
      JOIN dw.dim_producto p ON p.id_producto = o.id_producto
      JOIN dw.dim_fuente   f ON f.canal = o.canal
                            AND f.tipo_fuente = CASE o.origen WHEN 'Encuesta' THEN 'Encuesta'
                                                              WHEN 'Web' THEN 'Reseña web' ELSE 'Red social' END
      LEFT JOIN dw.dim_cliente c ON c.id_cliente = o.id_cliente
      LEFT JOIN dw.dim_sentimiento s ON s.nombre = o.sentimiento
      LEFT JOIN LATERAL (
            SELECT e.sk_evento
              FROM dw.dim_evento e
             WHERE e.sk_evento <> 0 AND o.fecha BETWEEN e.fecha_inicio AND e.fecha_fin
             ORDER BY e.fecha_inicio DESC
             LIMIT 1
      ) ev ON TRUE
     WHERE p.sk_producto IS NOT NULL
       AND (o.id_cliente IS NULL OR c.sk_cliente IS NOT NULL)
    ON CONFLICT (origen, id_origen) DO NOTHING;
    GET DIAGNOSTICS p_hechos = ROW_COUNT;
END;
$$;
