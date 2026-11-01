// メソッド説明:
// 頂点の位置を時間と位置に応じて"ウネウネ"と動かす
// 引数:
// - positionOS: 頂点のオブジェクト空間座標
// 戻り値:
// - 頂点の新しいオブジェクト空間座標
float4 applyUneuneEffect(float4 positionOS, float3 normalOS, float2 uv) {
    
    // 時間と位置に基づいてウネウネの振幅を計算
    float uneuneAmplitude = sin(_Time.y * _UneuneSpeed + positionOS.z * _UneunePosition);

    // 振幅にパワーを適用
    uneuneAmplitude = abs(uneuneAmplitude) * _UneunePower;

    // 頂点位置にウネウネ効果を適用
    positionOS.xyz -= normalOS * uneuneAmplitude * tex2Dlod(_Mask,float4(uv,0,0)).r;

    return positionOS;
}

// メソッド説明:
// カメラより後ろに回り込んだ頂点をカメラの前に移動する
// 引数:
// - positionOS: 頂点のオブジェクト空間座標
// 戻り値:
// - 頂点の新しいオブジェクト空間座標
float4 vertexMove(float4 positionOS, float2 uv) {
    
    // オブジェクト空間座標をワールド空間座標に変換
    float3 worldPos = mul(unity_ObjectToWorld, positionOS).xyz;

    // カメラから頂点への方向ベクトルを計算
    float3 vectorToVertex = worldPos - _WorldSpaceCameraPos;

    // カメラの前方向ベクトルを取得
    float3 cameraForwardDir = -UNITY_MATRIX_V[2].xyz;

    // カメラの前方向ベクトルと頂点への方向ベクトルのコサイン角度を計算
    float cosAngle = dot(normalize(cameraForwardDir), normalize(vectorToVertex));

    // 頂点がカメラの背後にある場合
    if(cosAngle < 0.0f) {

        // カメラから頂点までの距離を計算
        float distanceToVertex = length(vectorToVertex);

        // 最小移動距離を計算
        float minMoveDistance = distanceToVertex * abs(cosAngle);

        // カメラの前方向単位ベクトルを計算
        float3 unitCameraForward = normalize(cameraForwardDir);

        // 移動距離をスケーリング
        minMoveDistance *= max((_BehindDistance - minMoveDistance) / _BehindDistance, 0.0f);

        // 最終的な移動ベクトルを計算
        float3 finalMoveVector = unitCameraForward * minMoveDistance * _FlippingDistanceCoefficient + unitCameraForward * _MinimumDistance * tex2Dlod(_Mask,float4(uv,0,0)).r;

        // 頂点を移動
        worldPos += finalMoveVector;

        // ワールド空間座標をオブジェクト空間座標に逆変換
        positionOS = mul(unity_WorldToObject, float4(worldPos, 1.0f));
    }
    else if(length(vectorToVertex) < _MinimumDistance){
        // カメラから頂点までの距離を計算
        float distanceToVertex = length(vectorToVertex);

        // 最小移動距離を計算
        float minMoveDistance = _MinimumDistance - distanceToVertex * abs(cosAngle);

        // カメラの前方向単位ベクトルを計算
        float3 unitCameraForward = normalize(cameraForwardDir);

        // 移動距離をスケーリング
        // minMoveDistance *= 1.0f - distanceToVertex / _MinimumDistance;

         // 最終的な移動ベクトルを計算
        float3 finalMoveVector = unitCameraForward * minMoveDistance * tex2Dlod(_Mask,float4(uv,0,0)).r;

        // 頂点を移動
        worldPos += finalMoveVector;

        // ワールド空間座標をオブジェクト空間座標に逆変換
        positionOS = mul(unity_WorldToObject, float4(worldPos, 1.0f));
    }

    return positionOS;
}
