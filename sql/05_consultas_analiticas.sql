-- =====================================================================
-- Consultas del modelo en estrella: responden las preguntas del enunciado
-- Ejecutar después de:  CALL dw.sp_cargar_dw();
-- (el número entre corchetes es el de la pregunta en el PDF)
-- =====================================================================

-- ===== 1. ANÁLISIS GENERAL =====
-- [1.1][1.2][1.4] Total de comentarios, promedio de satisfacción y % por sentimiento
SELECT * FROM dw.vw_kpi_general;

-- [1.3] Comentarios por fuente de datos
SELECT tipo_fuente, SUM(total_comentarios) AS total_comentarios
FROM dw.vw_por_fuente
GROUP BY tipo_fuente
ORDER BY total_comentarios DESC;

-- ===== 2. OPINIONES POR PRODUCTO =====
-- [2.1] Producto con más comentarios
SELECT producto, categoria, total_comentarios
FROM dw.vw_producto_resumen
ORDER BY total_comentarios DESC, producto
LIMIT 5;

-- [2.2] Mejor calificación promedio (solo cuentan encuestas y reseñas, que tienen puntaje)
SELECT producto, categoria, puntaje_promedio, total_comentarios
FROM dw.vw_producto_resumen
WHERE puntaje_promedio IS NOT NULL
ORDER BY puntaje_promedio DESC, total_comentarios DESC
LIMIT 5;

-- [2.3] Productos con más opiniones negativas
SELECT producto, categoria, negativas, total_comentarios
FROM dw.vw_producto_resumen
ORDER BY negativas DESC, producto
LIMIT 5;

-- [2.4] Variación de la satisfacción de un producto en el tiempo (cambiar el nombre)
SELECT producto, anio_mes, total_comentarios, puntaje_promedio, pct_satisfaccion
FROM dw.vw_producto_mensual
WHERE producto = (SELECT producto FROM dw.vw_producto_resumen ORDER BY total_comentarios DESC LIMIT 1)
ORDER BY anio_mes;

-- [2.5] Porcentaje de satisfacción por producto (satisfacción = % de opiniones positivas)
SELECT producto, categoria, total_comentarios, pct_satisfaccion
FROM dw.vw_producto_resumen
ORDER BY pct_satisfaccion DESC, producto;

-- ===== 3. CLIENTES Y SEGMENTOS =====
-- [3.1] Clientes que más opinan
SELECT cliente, total_opiniones, puntaje_promedio
FROM dw.vw_cliente_actividad
ORDER BY total_opiniones DESC, cliente
LIMIT 10;

-- [3.2][3.3] Satisfacción por país / edad / tipo de cliente
-- Los CSV no incluyen estos datos: hoy devuelve una sola fila "No disponible".
SELECT * FROM dw.vw_segmento_cliente ORDER BY pct_positivas DESC;

-- Comentarios anónimos vs identificados
SELECT CASE WHEN sk_cliente = 0 THEN 'Anónimo' ELSE 'Identificado' END AS tipo, COUNT(*) AS comentarios
FROM dw.fact_opinion
GROUP BY 1;

-- ===== 4. TENDENCIAS EN EL TIEMPO =====
-- [4.1] Percepción mes a mes
SELECT anio_mes, total_comentarios, puntaje_promedio, pct_positivas, pct_neutras, pct_negativas
FROM dw.vw_tendencia_mensual
ORDER BY anio, mes;

-- [4.1] Percepción trimestre a trimestre
SELECT anio_trimestre, total_comentarios, puntaje_promedio, pct_positivas, pct_negativas
FROM dw.vw_tendencia_trimestral
ORDER BY anio, trimestre;

-- [4.2] Eventos o campañas que generaron más comentarios (hoy no hay eventos registrados)
SELECT * FROM dw.vw_evento_resumen ORDER BY total_comentarios DESC;

-- [4.3] ¿Mejora o deterioro? Variación mensual del % de opiniones positivas
SELECT anio_mes,
       pct_positivas,
       pct_positivas - LAG(pct_positivas) OVER (ORDER BY anio, mes) AS variacion_vs_mes_anterior
FROM dw.vw_tendencia_mensual
ORDER BY anio, mes;

-- ===== 5. CLASIFICACIÓN DE SENTIMIENTOS =====
-- [5.1][5.3] Cantidad y porcentaje por sentimiento
SELECT s.nombre AS sentimiento,
       COUNT(*) AS opiniones,
       ROUND(100.0 * COUNT(*) / SUM(COUNT(*)) OVER (), 1) AS porcentaje
FROM dw.fact_opinion f
JOIN dw.dim_sentimiento s ON s.sk_sentimiento = f.sk_sentimiento
GROUP BY s.nombre
ORDER BY opiniones DESC;

-- [5.2] (Opcional) Palabras más frecuentes en opiniones positivas y negativas
WITH palabras AS (
    SELECT s.nombre AS sentimiento,
           p.palabra
    FROM dw.fact_opinion f
    JOIN dw.dim_sentimiento s ON s.sk_sentimiento = f.sk_sentimiento
    CROSS JOIN LATERAL regexp_split_to_table(
        regexp_replace(lower(f.comentario), '[^a-záéíóúñü ]', ' ', 'g'), '\s+') AS p(palabra)
    WHERE s.nombre IN ('Positiva', 'Negativa')
      AND length(p.palabra) > 4          -- descarta artículos y conectores cortos
),
conteo AS (
    SELECT sentimiento, palabra, COUNT(*) AS apariciones
    FROM palabras
    GROUP BY sentimiento, palabra
),
ranking AS (
    SELECT *, ROW_NUMBER() OVER (PARTITION BY sentimiento ORDER BY apariciones DESC, palabra) AS puesto
    FROM conteo
)
SELECT sentimiento, puesto, palabra, apariciones
FROM ranking
WHERE puesto <= 10
ORDER BY sentimiento, puesto;

-- ===== 6. COMPARATIVA ENTRE CANALES =====
-- [6.1][6.2][6.3] Volumen, tono y proporción de negativos por canal
SELECT tipo_fuente, canal, total_comentarios, pct_positivas, pct_negativas, puntaje_promedio
FROM dw.vw_por_fuente
ORDER BY total_comentarios DESC;

-- [6.3] Canal con mayor proporción de comentarios negativos
SELECT tipo_fuente, canal, pct_negativas
FROM dw.vw_por_fuente
ORDER BY pct_negativas DESC
LIMIT 1;

-- ===== CONTROLES DE INTEGRIDAD DEL MODELO =====
-- Hechos sin dimensión asociada (esperado: 0 en todas las filas)
SELECT 'hechos sin tiempo' AS control, COUNT(*) AS cantidad
FROM dw.fact_opinion f LEFT JOIN dw.dim_tiempo t ON t.id_tiempo = f.id_tiempo WHERE t.id_tiempo IS NULL
UNION ALL
SELECT 'hechos sin producto', COUNT(*)
FROM dw.fact_opinion f LEFT JOIN dw.dim_producto p ON p.sk_producto = f.sk_producto WHERE p.sk_producto IS NULL
UNION ALL
SELECT 'hechos sin cliente', COUNT(*)
FROM dw.fact_opinion f LEFT JOIN dw.dim_cliente c ON c.sk_cliente = f.sk_cliente WHERE c.sk_cliente IS NULL
UNION ALL
SELECT 'hechos sin fuente', COUNT(*)
FROM dw.fact_opinion f LEFT JOIN dw.dim_fuente d ON d.sk_fuente = f.sk_fuente WHERE d.sk_fuente IS NULL
UNION ALL
SELECT 'hechos sin sentimiento', COUNT(*)
FROM dw.fact_opinion f LEFT JOIN dw.dim_sentimiento s ON s.sk_sentimiento = f.sk_sentimiento WHERE s.sk_sentimiento IS NULL;

-- Cuadre con el esquema transaccional (esperado: ambas cifras iguales)
SELECT (SELECT COUNT(*) FROM opiniones.vw_opiniones_consolidadas) AS opiniones_transaccional,
       (SELECT COUNT(*) FROM dw.fact_opinion)                     AS hechos_dw;
