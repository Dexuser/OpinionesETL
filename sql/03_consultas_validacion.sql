-- =====================================================================
-- Script 03: consultas de validación posteriores a la carga
-- Los valores "esperados" corresponden a los CSV entregados, con las reglas del ETL.
-- =====================================================================
SET search_path TO opiniones;

-- 1) Filas cargadas por tabla
--    Esperado: fuente_datos 100, cliente 500, producto 200, resena_web 200,
--              comentario_social 200, encuesta 6, etl_registro_rechazado 494
SELECT * FROM opiniones.vw_resumen_carga ORDER BY tabla;

-- 2) Cuadre por archivo: procesados (filas del CSV) = insertados + rechazados
--    Esperado: la columna "cuadra" en true para los seis archivos
SELECT archivo,
       esperado_csv,
       insertados,
       rechazados,
       insertados + rechazados        AS suma,
       insertados + rechazados = esperado_csv AS cuadra
FROM (
    SELECT 'fuente_datos.csv' AS archivo, 100 AS esperado_csv,
           (SELECT COUNT(*) FROM opiniones.fuente_datos)       AS insertados,
           (SELECT COUNT(*) FROM opiniones.etl_registro_rechazado WHERE archivo_origen = 'fuente_datos.csv') AS rechazados
    UNION ALL SELECT 'clients.csv', 500,
           (SELECT COUNT(*) FROM opiniones.cliente),
           (SELECT COUNT(*) FROM opiniones.etl_registro_rechazado WHERE archivo_origen = 'clients.csv')
    UNION ALL SELECT 'products.csv', 200,
           (SELECT COUNT(*) FROM opiniones.producto),
           (SELECT COUNT(*) FROM opiniones.etl_registro_rechazado WHERE archivo_origen = 'products.csv')
    UNION ALL SELECT 'web_reviews.csv', 200,
           (SELECT COUNT(*) FROM opiniones.resena_web),
           (SELECT COUNT(*) FROM opiniones.etl_registro_rechazado WHERE archivo_origen = 'web_reviews.csv')
    UNION ALL SELECT 'social_comments.csv', 200,
           (SELECT COUNT(*) FROM opiniones.comentario_social),
           (SELECT COUNT(*) FROM opiniones.etl_registro_rechazado WHERE archivo_origen = 'social_comments.csv')
    UNION ALL SELECT 'surveys_part1.csv', 500,
           (SELECT COUNT(*) FROM opiniones.encuesta),
           (SELECT COUNT(*) FROM opiniones.etl_registro_rechazado WHERE archivo_origen = 'surveys_part1.csv')
) t
ORDER BY archivo;

-- 3) Integridad referencial: registros huérfanos (esperado: cantidad = 0 en todas las filas)
SELECT 'encuesta sin cliente' AS control, COUNT(*) AS cantidad
FROM opiniones.encuesta e LEFT JOIN opiniones.cliente c ON c.id_cliente = e.id_cliente
WHERE c.id_cliente IS NULL
UNION ALL
SELECT 'encuesta sin producto', COUNT(*)
FROM opiniones.encuesta e LEFT JOIN opiniones.producto p ON p.id_producto = e.id_producto
WHERE p.id_producto IS NULL
UNION ALL
SELECT 'resena_web sin cliente', COUNT(*)
FROM opiniones.resena_web r LEFT JOIN opiniones.cliente c ON c.id_cliente = r.id_cliente
WHERE c.id_cliente IS NULL
UNION ALL
SELECT 'resena_web sin producto', COUNT(*)
FROM opiniones.resena_web r LEFT JOIN opiniones.producto p ON p.id_producto = r.id_producto
WHERE p.id_producto IS NULL
UNION ALL
SELECT 'comentario_social con cliente inexistente', COUNT(*)
FROM opiniones.comentario_social s LEFT JOIN opiniones.cliente c ON c.id_cliente = s.id_cliente
WHERE s.id_cliente IS NOT NULL AND c.id_cliente IS NULL
UNION ALL
SELECT 'comentario_social sin producto', COUNT(*)
FROM opiniones.comentario_social s LEFT JOIN opiniones.producto p ON p.id_producto = s.id_producto
WHERE p.id_producto IS NULL
UNION ALL
SELECT 'producto sin categoria', COUNT(*)
FROM opiniones.producto p LEFT JOIN opiniones.categoria_producto c ON c.id_categoria = p.id_categoria
WHERE c.id_categoria IS NULL
UNION ALL
SELECT 'fuente_datos sin tipo', COUNT(*)
FROM opiniones.fuente_datos f LEFT JOIN opiniones.tipo_fuente t ON t.id_tipo_fuente = f.id_tipo_fuente
WHERE t.id_tipo_fuente IS NULL;

-- 4) Duplicados (esperado: cantidad = 0 en todas las filas)
SELECT 'cliente: email repetido' AS control, COUNT(*) AS cantidad
FROM (SELECT email FROM opiniones.cliente GROUP BY email HAVING COUNT(*) > 1) d
UNION ALL
SELECT 'resena_web: id_review repetido', COUNT(*)
FROM (SELECT id_review FROM opiniones.resena_web GROUP BY id_review HAVING COUNT(*) > 1) d
UNION ALL
SELECT 'comentario_social: id_comment repetido', COUNT(*)
FROM (SELECT id_comment FROM opiniones.comentario_social GROUP BY id_comment HAVING COUNT(*) > 1) d
UNION ALL
SELECT 'encuesta: id_opinion repetido', COUNT(*)
FROM (SELECT id_opinion FROM opiniones.encuesta GROUP BY id_opinion HAVING COUNT(*) > 1) d;

-- 5) Clasificación completa y coherente con el puntaje (esperado: cantidad = 0 en todas las filas)
SELECT 'resena_web sin clasificacion' AS control, COUNT(*) AS cantidad
FROM opiniones.resena_web WHERE id_clasificacion IS NULL
UNION ALL
SELECT 'comentario_social sin clasificacion', COUNT(*)
FROM opiniones.comentario_social WHERE id_clasificacion IS NULL
UNION ALL
SELECT 'encuesta sin clasificacion', COUNT(*)
FROM opiniones.encuesta WHERE id_clasificacion IS NULL
UNION ALL
SELECT 'encuesta con clasificacion incoherente', COUNT(*)
FROM opiniones.encuesta e
JOIN opiniones.clasificacion_sentimiento c ON c.id_clasificacion = e.id_clasificacion
WHERE c.nombre <> opiniones.fn_clasificar_puntaje(e.puntaje_satisfaccion)
UNION ALL
SELECT 'resena_web con clasificacion incoherente', COUNT(*)
FROM opiniones.resena_web r
JOIN opiniones.clasificacion_sentimiento c ON c.id_clasificacion = r.id_clasificacion
WHERE c.nombre <> opiniones.fn_clasificar_puntaje(r.rating);

-- 6) Rechazos por archivo y motivo
--    Esperado: surveys_part1.csv concentra los 494 rechazos (IdCliente / IdProducto que no existen)
SELECT * FROM opiniones.vw_rechazos_por_motivo ORDER BY archivo_origen, cantidad DESC;

-- 7) Distribución del sentimiento por origen y canal
SELECT * FROM opiniones.vw_satisfaccion_por_origen ORDER BY origen, canal;

-- 8) Productos con más opiniones negativas
SELECT producto, categoria, total_opiniones, negativas, pct_positivas
FROM opiniones.vw_sentimiento_por_producto
ORDER BY negativas DESC, producto
LIMIT 5;

-- 9) Claves primarias y foráneas definidas en cada tabla
SELECT table_name AS tabla, constraint_type AS tipo, COUNT(*) AS cantidad
FROM information_schema.table_constraints
WHERE table_schema = 'opiniones'
  AND constraint_type IN ('PRIMARY KEY', 'FOREIGN KEY')
GROUP BY table_name, constraint_type
ORDER BY table_name, constraint_type;
