-- =====================================================================
-- Script 02: función, procedimientos almacenados y vistas
-- Ejecutar conectado a "opiniones_clientes", DESPUÉS de 01_crear_base_datos.sql
-- En DBeaver: Ejecutar script (Alt+X)
-- =====================================================================
SET search_path TO opiniones;

-- ---------------------------------------------------------------------
-- FUNCIÓN: regla de clasificación por puntaje (1-2 Negativa, 3 Neutra, 4-5 Positiva)
-- ---------------------------------------------------------------------
CREATE OR REPLACE FUNCTION opiniones.fn_clasificar_puntaje(p_puntaje SMALLINT)
RETURNS VARCHAR(10)
LANGUAGE sql
IMMUTABLE
AS $$
    SELECT CASE
               WHEN p_puntaje <= 2 THEN 'Negativa'
               WHEN p_puntaje = 3  THEN 'Neutra'
               ELSE 'Positiva'
           END;
$$;

-- ---------------------------------------------------------------------
-- PROCEDIMIENTO: completa id_clasificacion en reseñas web y encuestas
-- que aún no la tienen, usando su puntaje. El ETL lo llama al final de la carga.
-- ---------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE opiniones.sp_clasificar_por_puntaje()
LANGUAGE plpgsql
AS $$
DECLARE
    v_resenas   INTEGER;
    v_encuestas INTEGER;
BEGIN
    UPDATE opiniones.resena_web r
       SET id_clasificacion = c.id_clasificacion
      FROM opiniones.clasificacion_sentimiento c
     WHERE r.id_clasificacion IS NULL
       AND c.nombre = opiniones.fn_clasificar_puntaje(r.rating);
    GET DIAGNOSTICS v_resenas = ROW_COUNT;

    UPDATE opiniones.encuesta e
       SET id_clasificacion = c.id_clasificacion
      FROM opiniones.clasificacion_sentimiento c
     WHERE e.id_clasificacion IS NULL
       AND c.nombre = opiniones.fn_clasificar_puntaje(e.puntaje_satisfaccion);
    GET DIAGNOSTICS v_encuestas = ROW_COUNT;

    RAISE NOTICE 'Clasificadas: % reseñas web, % encuestas', v_resenas, v_encuestas;
END;
$$;

-- ---------------------------------------------------------------------
-- PROCEDIMIENTO: vacía las tablas de datos para repetir la carga desde cero.
-- No toca los catálogos (categoría, tipo de fuente, red social, clasificación).
-- ---------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE opiniones.sp_reiniciar_carga()
LANGUAGE plpgsql
AS $$
BEGIN
    TRUNCATE TABLE
        opiniones.comentario_social,
        opiniones.resena_web,
        opiniones.encuesta,
        opiniones.producto,
        opiniones.cliente,
        opiniones.fuente_datos,
        opiniones.etl_registro_rechazado
    RESTART IDENTITY;

    RAISE NOTICE 'Tablas de datos vaciadas. Los catálogos se conservan.';
END;
$$;

-- ---------------------------------------------------------------------
-- VISTA: opiniones de las tres fuentes con nombres legibles (para Power BI)
-- ---------------------------------------------------------------------
CREATE OR REPLACE VIEW opiniones.vw_opiniones_detalle AS
SELECT o.origen,
       o.id_origen,
       o.canal,
       o.fecha,
       c.id_cliente,
       c.nombre   AS cliente,
       p.id_producto,
       p.nombre   AS producto,
       cat.nombre AS categoria,
       cl.nombre  AS clasificacion,
       o.puntaje,
       o.comentario
FROM opiniones.vw_opiniones_consolidadas o
JOIN opiniones.producto p                       ON p.id_producto = o.id_producto
JOIN opiniones.categoria_producto cat           ON cat.id_categoria = p.id_categoria
LEFT JOIN opiniones.cliente c                   ON c.id_cliente = o.id_cliente
LEFT JOIN opiniones.clasificacion_sentimiento cl ON cl.id_clasificacion = o.id_clasificacion;

-- ---------------------------------------------------------------------
-- VISTA: sentimiento por producto
-- ---------------------------------------------------------------------
CREATE OR REPLACE VIEW opiniones.vw_sentimiento_por_producto AS
SELECT id_producto,
       producto,
       categoria,
       COUNT(*)                                            AS total_opiniones,
       COUNT(*) FILTER (WHERE clasificacion = 'Positiva')  AS positivas,
       COUNT(*) FILTER (WHERE clasificacion = 'Neutra')    AS neutras,
       COUNT(*) FILTER (WHERE clasificacion = 'Negativa')  AS negativas,
       ROUND(100.0 * COUNT(*) FILTER (WHERE clasificacion = 'Positiva') / COUNT(*), 1) AS pct_positivas,
       ROUND(AVG(puntaje), 2)                              AS puntaje_promedio
FROM opiniones.vw_opiniones_detalle
GROUP BY id_producto, producto, categoria;

-- ---------------------------------------------------------------------
-- VISTA: satisfacción por origen y canal
-- ---------------------------------------------------------------------
CREATE OR REPLACE VIEW opiniones.vw_satisfaccion_por_origen AS
SELECT origen,
       canal,
       COUNT(*)                                            AS total_opiniones,
       COUNT(*) FILTER (WHERE clasificacion = 'Positiva')  AS positivas,
       COUNT(*) FILTER (WHERE clasificacion = 'Neutra')    AS neutras,
       COUNT(*) FILTER (WHERE clasificacion = 'Negativa')  AS negativas,
       ROUND(100.0 * COUNT(*) FILTER (WHERE clasificacion = 'Positiva') / COUNT(*), 1) AS pct_positivas,
       ROUND(AVG(puntaje), 2)                              AS puntaje_promedio
FROM opiniones.vw_opiniones_detalle
GROUP BY origen, canal;

-- ---------------------------------------------------------------------
-- VISTA: filas por tabla (resumen rápido de lo cargado)
-- ---------------------------------------------------------------------
CREATE OR REPLACE VIEW opiniones.vw_resumen_carga AS
SELECT 'fuente_datos'           AS tabla, COUNT(*) AS filas FROM opiniones.fuente_datos
UNION ALL SELECT 'cliente',               COUNT(*) FROM opiniones.cliente
UNION ALL SELECT 'producto',              COUNT(*) FROM opiniones.producto
UNION ALL SELECT 'resena_web',            COUNT(*) FROM opiniones.resena_web
UNION ALL SELECT 'comentario_social',     COUNT(*) FROM opiniones.comentario_social
UNION ALL SELECT 'encuesta',              COUNT(*) FROM opiniones.encuesta
UNION ALL SELECT 'etl_registro_rechazado',COUNT(*) FROM opiniones.etl_registro_rechazado;

-- ---------------------------------------------------------------------
-- VISTA: rechazos agrupados por archivo y motivo (los números del motivo se enmascaran con #)
-- ---------------------------------------------------------------------
CREATE OR REPLACE VIEW opiniones.vw_rechazos_por_motivo AS
SELECT archivo_origen,
       regexp_replace(motivo, '''[^'']*''|[0-9]+', '#', 'g') AS motivo,
       COUNT(*) AS cantidad
FROM opiniones.etl_registro_rechazado
GROUP BY archivo_origen, regexp_replace(motivo, '''[^'']*''|[0-9]+', '#', 'g');
