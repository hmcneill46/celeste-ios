#!/usr/bin/env swift

import CoreGraphics
import Foundation
import ImageIO
import UniformTypeIdentifiers

enum IconError: Error, CustomStringConvertible {
    case message(String)

    var description: String {
        switch self {
        case .message(let value): return value
        }
    }
}

func load(_ path: String) throws -> CGImage {
    let url = URL(fileURLWithPath: path) as CFURL
    guard let source = CGImageSourceCreateWithURL(url, nil),
          CGImageSourceGetCount(source) == 1,
          let image = CGImageSourceCreateImageAtIndex(source, 0, nil) else {
        throw IconError.message("could not decode app icon: \(path)")
    }
    return image
}

func bitmapContext(width: Int, height: Int, storesAlpha: Bool) throws -> CGContext {
    let bytesPerPixel = 4
    let bitmapInfo = storesAlpha
        ? CGImageAlphaInfo.premultipliedLast.rawValue | CGBitmapInfo.byteOrder32Big.rawValue
        : CGImageAlphaInfo.noneSkipFirst.rawValue | CGBitmapInfo.byteOrder32Little.rawValue
    guard let colourSpace = CGColorSpace(name: CGColorSpace.sRGB),
          let context = CGContext(
              data: nil,
              width: width,
              height: height,
              bitsPerComponent: 8,
              bytesPerRow: width * bytesPerPixel,
              space: colourSpace,
              bitmapInfo: bitmapInfo
          ) else {
        throw IconError.message("could not create app-icon bitmap")
    }
    return context
}

func render(_ image: CGImage, blackBackground: Bool) throws -> CGContext {
    let context = try bitmapContext(
        width: image.width,
        height: image.height,
        storesAlpha: !blackBackground
    )
    if blackBackground {
        context.setFillColor(red: 0, green: 0, blue: 0, alpha: 1)
        context.fill(CGRect(x: 0, y: 0, width: image.width, height: image.height))
    } else {
        context.clear(CGRect(x: 0, y: 0, width: image.width, height: image.height))
    }
    context.interpolationQuality = .none
    context.draw(image, in: CGRect(x: 0, y: 0, width: image.width, height: image.height))
    return context
}

func verifyOpaqueBlackCorners(_ image: CGImage) throws {
    let context = try render(image, blackBackground: false)
    guard let raw = context.data else {
        throw IconError.message("could not inspect app-icon pixels")
    }
    let bytes = raw.bindMemory(to: UInt8.self, capacity: image.width * image.height * 4)
    for pixel in 0..<(image.width * image.height) where bytes[pixel * 4 + 3] != 255 {
        throw IconError.message("app icon contains a non-opaque pixel")
    }
    let corners = [0, image.width - 1, (image.height - 1) * image.width,
                   image.height * image.width - 1]
    for pixel in corners {
        let offset = pixel * 4
        if bytes[offset] != 0 || bytes[offset + 1] != 0 || bytes[offset + 2] != 0 {
            throw IconError.message("app icon does not have black corners")
        }
    }
}

func writePNG(_ context: CGContext, to path: String) throws {
    guard let image = context.makeImage() else {
        throw IconError.message("could not finalize app-icon bitmap")
    }
    let url = URL(fileURLWithPath: path)
    try FileManager.default.createDirectory(
        at: url.deletingLastPathComponent(),
        withIntermediateDirectories: true
    )
    guard let destination = CGImageDestinationCreateWithURL(
        url as CFURL,
        UTType.png.identifier as CFString,
        1,
        nil
    ) else {
        throw IconError.message("could not create app-icon PNG destination")
    }
    CGImageDestinationAddImage(destination, image, nil)
    guard CGImageDestinationFinalize(destination) else {
        throw IconError.message("could not write app-icon PNG")
    }
}

func value(after option: String, in arguments: [String]) -> String? {
    guard let index = arguments.firstIndex(of: option), index + 1 < arguments.count else {
        return nil
    }
    return arguments[index + 1]
}

do {
    let arguments = Array(CommandLine.arguments.dropFirst())
    if let path = value(after: "--verify", in: arguments), arguments.count == 2 {
        try verifyOpaqueBlackCorners(try load(path))
        print("verified opaque black iOS app icon: \(path)")
    } else if let input = value(after: "--input", in: arguments),
              let output = value(after: "--output", in: arguments),
              arguments.count == 4 {
        let source = try load(input)
        guard source.width == 1024 && source.height == 1024 else {
            throw IconError.message("source app icon must be exactly 1024 x 1024")
        }
        try writePNG(try render(source, blackBackground: true), to: output)
        try verifyOpaqueBlackCorners(try load(output))
        print("generated opaque-black iOS app icon: \(output)")
    } else {
        throw IconError.message(
            "usage: generate-ios-app-icon.swift --input SOURCE --output PNG | --verify PNG"
        )
    }
} catch {
    FileHandle.standardError.write(Data("error: \(error)\n".utf8))
    exit(1)
}
