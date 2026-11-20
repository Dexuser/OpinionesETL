-- =====================================================================
-- Sistema de Análisis de Opiniones de Clientes  (PostgreSQL 14+)
-- Script 01: creación del esquema y las tablas
-- Compatible con DBeaver / pgAdmin: ejecutar CONECTADO a la base de datos
-- "opiniones_clientes" (créala antes con 00_crear_base_datos.sql).
-- En DBeaver usa "Ejecutar script" (Alt+X), no Ctrl+Enter.
-- Se puede volver a ejecutar: borra y recrea el esquema "opiniones".
-- =====================================================================
DROP SCHEMA IF EXISTS opiniones CASCADE;
CREATE SCHEMA IF NOT EXISTS opiniones;
SET search_path TO opiniones;

-- ---------------------------------------------------------------------
-- 1. CATÁLOGOS
-- ---------------------------------------------------------------------
CREATE TABLE categoria_producto (
    id_categoria  SMALLINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    nombre        VARCHAR(50) NOT NULL UNIQUE          -- products.csv: Categoría
);

CREATE TABLE tipo_fuente (
    id_tipo_fuente SMALLINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    nombre         VARCHAR(30) NOT NULL UNIQUE         -- fuente_datos.csv: Web, CSV, Red Social
);

CREATE TABLE red_social (
    id_red_social SMALLINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    nombre        VARCHAR(30) NOT NULL UNIQUE          -- social_comments.csv: Instagram, Facebook, Twitter
);

CREATE TABLE clasificacion_sentimiento (
    id_clasificacion SMALLINT PRIMARY KEY,
    nombre           VARCHAR(10) NOT NULL UNIQUE,      -- Positiva, Neutra, Negativa
    CONSTRAINT ck_clasificacion_nombre CHECK (nombre IN ('Positiva','Neutra','Negativa'))
);

-- ---------------------------------------------------------------------
-- 2. ENTIDADES PRINCIPALES
-- ---------------------------------------------------------------------
CREATE TABLE cliente (
    id_cliente INTEGER      PRIMARY KEY,               -- clients.csv: IdCliente
    nombre     VARCHAR(100) NOT NULL,
    email      VARCHAR(150) NOT NULL UNIQUE
);

CREATE TABLE producto (
    id_producto  INTEGER      PRIMARY KEY,             -- products.csv: IdProducto
    nombre       VARCHAR(100) NOT NULL,
    id_categoria SMALLINT     NOT NULL,
    CONSTRAINT fk_producto_categoria FOREIGN KEY (id_categoria)
        REFERENCES categoria_producto (id_categoria)
);

CREATE TABLE fuente_datos (                            -- lotes de carga (fuente_datos.csv)
    id_fuente      CHAR(4)  PRIMARY KEY,               -- F001 ... F100
    id_tipo_fuente SMALLINT NOT NULL,
    fecha_carga    DATE     NOT NULL,
    CONSTRAINT fk_fuente_tipo FOREIGN KEY (id_tipo_fuente)
        REFERENCES tipo_fuente (id_tipo_fuente)
);

-- ---------------------------------------------------------------------
-- 3. OPINIONES (una tabla por origen)
--    id_clasificacion es NULL hasta que el ETL la calcula
--    (solo las encuestas la traen en el CSV).
-- ---------------------------------------------------------------------
CREATE TABLE encuesta (                                -- surveys_part1.csv
    id_opinion            INTEGER      PRIMARY KEY,
    id_cliente            INTEGER      NOT NULL,
    id_producto           INTEGER      NOT NULL,
    fecha                 DATE         NOT NULL,
    comentario            VARCHAR(500) NOT NULL,
    id_clasificacion      SMALLINT,
    puntaje_satisfaccion  SMALLINT     NOT NULL,
    fuente                VARCHAR(30)  NOT NULL DEFAULT 'EncuestaInterna',
    id_fuente             CHAR(4),
    CONSTRAINT fk_encuesta_cliente  FOREIGN KEY (id_cliente)       REFERENCES cliente (id_cliente),
    CONSTRAINT fk_encuesta_producto FOREIGN KEY (id_producto)      REFERENCES producto (id_producto),
    CONSTRAINT fk_encuesta_clasif   FOREIGN KEY (id_clasificacion) REFERENCES clasificacion_sentimiento (id_clasificacion),
    CONSTRAINT fk_encuesta_fuente   FOREIGN KEY (id_fuente)        REFERENCES fuente_datos (id_fuente),
    CONSTRAINT ck_encuesta_puntaje  CHECK (puntaje_satisfaccion BETWEEN 1 AND 5)
);

CREATE TABLE resena_web (                              -- web_reviews.csv
    id_review        CHAR(5)      PRIMARY KEY,         -- W0001 ...
    id_cliente       INTEGER      NOT NULL,            -- CSV trae 'C007' -> el ETL lo convierte a 7
    id_producto      INTEGER      NOT NULL,            -- CSV trae 'P016' -> 16
    fecha            DATE         NOT NULL,
    comentario       VARCHAR(500) NOT NULL,
    rating           SMALLINT     NOT NULL,
    id_clasificacion SMALLINT,
    id_fuente        CHAR(4),
    CONSTRAINT fk_resena_cliente  FOREIGN KEY (id_cliente)       REFERENCES cliente (id_cliente),
    CONSTRAINT fk_resena_producto FOREIGN KEY (id_producto)      REFERENCES producto (id_producto),
    CONSTRAINT fk_resena_clasif   FOREIGN KEY (id_clasificacion) REFERENCES clasificacion_sentimiento (id_clasificacion),
    CONSTRAINT fk_resena_fuente   FOREIGN KEY (id_fuente)        REFERENCES fuente_datos (id_fuente),
    CONSTRAINT ck_resena_rating   CHECK (rating BETWEEN 1 AND 5)
);

CREATE TABLE comentario_social (                       -- social_comments.csv
    id_comment       CHAR(5)      PRIMARY KEY,         -- T0001 ...
    id_cliente       INTEGER,                          -- NULL: 88 de 200 comentarios son anónimos
    id_producto      INTEGER      NOT NULL,
    id_red_social    SMALLINT     NOT NULL,
    fecha            DATE         NOT NULL,
    comentario       VARCHAR(500) NOT NULL,
    id_clasificacion SMALLINT,
    id_fuente        CHAR(4),
    CONSTRAINT fk_social_cliente  FOREIGN KEY (id_cliente)       REFERENCES cliente (id_cliente),
    CONSTRAINT fk_social_producto FOREIGN KEY (id_producto)      REFERENCES producto (id_producto),
    CONSTRAINT fk_social_red      FOREIGN KEY (id_red_social)    REFERENCES red_social (id_red_social),
    CONSTRAINT fk_social_clasif   FOREIGN KEY (id_clasificacion) REFERENCES clasificacion_sentimiento (id_clasificacion),
    CONSTRAINT fk_social_fuente   FOREIGN KEY (id_fuente)        REFERENCES fuente_datos (id_fuente)
);

-- ---------------------------------------------------------------------
-- 4. CONTROL DE CALIDAD DEL ETL
--    Filas cuyo cliente/producto no existe (o con datos inválidos) se
--    guardan aquí en lugar de perderse o romper la integridad referencial.
-- ---------------------------------------------------------------------
CREATE TABLE etl_registro_rechazado (
    id_rechazo       BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    archivo_origen   VARCHAR(60)  NOT NULL,            -- ej. surveys_part1.csv
    id_registro      VARCHAR(20),                      -- IdOpinion / IdReview / IdComment
    motivo           VARCHAR(200) NOT NULL,            -- ej. 'IdCliente 8537 no existe en cliente'
    datos_originales JSONB        NOT NULL,
    fecha_rechazo    TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- ---------------------------------------------------------------------
-- 5. ÍNDICES sobre claves foráneas (consultas del dashboard)
-- ---------------------------------------------------------------------
CREATE INDEX ix_producto_categoria ON producto (id_categoria);
CREATE INDEX ix_encuesta_cliente   ON encuesta (id_cliente);
CREATE INDEX ix_encuesta_producto  ON encuesta (id_producto);
CREATE INDEX ix_encuesta_fecha     ON encuesta (fecha);
CREATE INDEX ix_resena_cliente     ON resena_web (id_cliente);
CREATE INDEX ix_resena_producto    ON resena_web (id_producto);
CREATE INDEX ix_resena_fecha       ON resena_web (fecha);
CREATE INDEX ix_social_cliente     ON comentario_social (id_cliente);
CREATE INDEX ix_social_producto    ON comentario_social (id_producto);
CREATE INDEX ix_social_fecha       ON comentario_social (fecha);

-- ---------------------------------------------------------------------
-- 6. DATOS SEMILLA DE CATÁLOGOS
-- ---------------------------------------------------------------------
INSERT INTO clasificacion_sentimiento (id_clasificacion, nombre) VALUES (1,'Positiva'),(2,'Neutra'),(3,'Negativa');
INSERT INTO tipo_fuente (nombre) VALUES ('Web'),('CSV'),('Red Social');
INSERT INTO red_social  (nombre) VALUES ('Instagram'),('Facebook'),('Twitter');
INSERT INTO categoria_producto (nombre) VALUES ('Juguetes'),('Hogar'),('Electrónica'),('Deportes'),('Ropa');

-- ---------------------------------------------------------------------
-- 7. VISTA CONSOLIDADA para Power BI (las 3 fuentes con el mismo esquema)
-- ---------------------------------------------------------------------
CREATE VIEW vw_opiniones_consolidadas AS
SELECT 'Encuesta'::text AS origen, id_opinion::text AS id_origen, id_cliente, id_producto, fecha,
       comentario, id_clasificacion, puntaje_satisfaccion AS puntaje, fuente::text AS canal
FROM encuesta
UNION ALL
SELECT 'Web', id_review::text, id_cliente, id_producto, fecha,
       comentario, id_clasificacion, rating, 'Web'
FROM resena_web
UNION ALL
SELECT 'Social', c.id_comment::text, c.id_cliente, c.id_producto, c.fecha,
       c.comentario, c.id_clasificacion, NULL::smallint, r.nombre::text
FROM comentario_social c JOIN red_social r ON r.id_red_social = c.id_red_social;
